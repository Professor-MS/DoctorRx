# Architecture Decision Records (ADRs)

This document records architectural and technical decisions made during the DoctorRx Phase 1 foundation refactoring.

---

## ADR 001: SQLite Durability Pragmas (WAL + FULL Synchronous)
- **Status**: Accepted
- **Decision**: On every connection open, DoctorRx enforces:
  - `PRAGMA journal_mode=WAL;`
  - `PRAGMA foreign_keys=ON;`
  - `PRAGMA busy_timeout=5000;`
  - `PRAGMA synchronous=FULL;`
- **Reason**: 
  - Medical prescriptions and clinical records demand absolute durability over raw write speed. `synchronous=FULL` ensures that when a transaction is committed, changes are completely flushed to disk and cannot be lost due to sudden power outages or OS crashes.
  - In WAL mode, `FULL` sync synchronizes the WAL file on checkpoint and commit, preserving clinical records without corrupting write operations.
  - `foreign_keys=ON` is critical because SQLite disables foreign key checks by default.
  - `busy_timeout=5000` allows waiting up to 5 seconds if a background task holds the lock.
- **Alternatives Rejected**:
  - `synchronous=NORMAL`: Faster for bulk ingestion, but risks losing recent writes or WAL sync during sudden workstation power failure, which is unacceptable for medical records.
  - `synchronous=OFF`: Completely unsafe for production clinical systems.

---

## ADR 002: DbContext Lifetime via IUnitOfWorkFactory
- **Status**: Accepted
- **Decision**: Register `AddDbContextFactory<DoctorRxDbContext>` and provide `IUnitOfWorkFactory`. Application services become stateless singletons/transients where every method creates an isolated, short-lived Unit of Work (`await using var uow = _uowFactory.Create();`).
- **Reason**: 
  - In desktop WPF applications, resolving a scoped `DbContext` from the root provider causes the entire application to share a single long-lived context.
  - Keystroke searches, concurrent tasks, and failed saves would poison the shared ChangeTracker.
  - Short-lived contexts ensure complete isolation, zero state accumulation, and automatic disposal.
- **Alternatives Rejected**:
  - Long-lived root scoped DbContext: Disastrous in desktop apps with multi-threading and keystroke search.
  - Resetting ChangeTracker on failure (`ChangeTracker.Clear()`): Prone to subtle leaks and incomplete cleanup.

---

## ADR 003: AppPaths and Directory Hierarchy
- **Status**: Accepted
- **Decision**: Centralize all application paths in `AppPaths` behind `IAppPaths`, residing under `%LOCALAPPDATA%\DoctorRx\` with dedicated folders: `Data`, `Backups`, `Drafts`, `Logs`, `Assets`. Provide environment variable override (`DOCTORRX_DATA_DIR`) and constructor override for tests.
- **Reason**: 
  - Never hard-code Windows usernames or paths.
  - Test suites can redirect storage to isolated temporary directories.
  - Paths with spaces or special characters are safely escaped using `SqliteConnectionStringBuilder`.
- **Alternatives Rejected**:
  - Storing data alongside application binaries (`bin/` or `Program Files`): Windows permissions forbid writing to Program Files.
  - Ad-hoc `Path.Combine` across multiple classes: Unmaintainable and risk of path inconsistencies.

---

## ADR 004: Prescription Immutability and Snapshots
- **Status**: Accepted
- **Decision**: Finalized prescriptions are immutable. Clinical metadata, medicine items, doctor credentials, and patient details are snapshotted at finalization time. Read operations query the snapshots, not live doctor/patient rows.
- **Reason**: 
  - Medical and legal necessity: editing a doctor's qualification or patient's phone number years later must not alter the historical legal document.
- **Alternatives Rejected**:
  - Live relational queries to Doctor/Patient tables for historical records: Modifying master records silently alters past prescriptions.

---

## ADR 005: Atomic Number Sequencing with BEGIN IMMEDIATE
- **Status**: Accepted
- **Decision**: Generate prescription and patient sequential identifiers within the exact same database write transaction as the entity insertion, using SQLite `BEGIN IMMEDIATE` transaction semantics with up to 3 retries.
- **Reason**: 
  - Prevents burned numbers on failed saves (rolling back the transaction rolls back the sequence counter).
  - Serializes concurrent writers at the start of the transaction, eliminating race conditions.
- **Alternatives Rejected**:
  - In-memory sequence counter: Lost on crash; fails across multi-process instances.
  - Pre-allocated sequence on separate transaction: Leaves gaps (burned numbers) when prescription creation fails validation or is cancelled.

---

## ADR 006: Prescription Amendment Suffix Format
- **Status**: Accepted
- **Decision**: When amending a prescription, create a new record preserving the parent's base number with suffix `-A{AmendmentNumber}` (e.g. `RX-20261006-0001-A1`), while marking the parent as `Superseded`.
- **Reason**: 
  - Instantly reveals the audit relationship to pharmacists and medical auditors while maintaining visual continuity with the original prescription number.
- **Alternatives Rejected**:
  - Allocating completely unrelated new prescription numbers: Obscures which prescription is being amended.
  - Overwriting the existing prescription row: Violates medical immutability and audit trails.

---

## ADR 007: SQLite Triggers for Anti-Tamper Immutability and State Transitions
- **Status**: Accepted
- **Decision**: SQLite triggers enforce immutability at the physical storage engine layer:
  - `trg_prevent_prescription_tamper`: Prohibits updates to clinical and snapshot columns. Allows only valid status transitions (`Finalized` (1) -> `Cancelled` (2) or `Superseded` (3)). Allows `Version` (concurrency token) and `UpdatedAtUtc` to be incremented. Prohibits any updates once status is terminal (`Cancelled` or `Superseded`).
  - `trg_prevent_prescription_delete`: Prohibits deletion of prescription rows.
  - `trg_prevent_prescription_medicine_delete`: Prohibits deletion of prescribed medicines.
  - `trg_prevent_prescription_medicine_update`: Prohibits modifying prescribed medicine rows.
  - `trg_prevent_prescription_medicine_insert_after_terminal`: Prohibits inserting new medicine items into a cancelled or superseded prescription. Prescription items are created atomically alongside the prescription in a single creation transaction; subsequent amendment creates a fresh prescription.
  - `FK_PrescriptionMedicines_Medicines_MedicineId` configured with `ON DELETE RESTRICT` to prevent deleting medicines that have been prescribed.
- **Reason**:
  - Medical software cannot rely solely on in-memory application guards. Defense-in-depth requires that even raw SQL queries or buggy code cannot mutate historical medical charts.

---

## ADR 008: Safe Pre-Migration Backup, Retention, and Legacy Database Guard
- **Status**: Accepted
- **Decision**: 
  - `DatabaseMigrator` creates a pre-migration backup (`%LOCALAPPDATA%\DoctorRx\Backups\pre-migration-{timestamp}.db`) using SQLite Online Backup API ONLY when an existing database has pending migrations (never on fresh setup).
  - Keeps only the last 5 pre-migration backups, automatically pruning older ones.
  - If a migration fails, the migrator clears connection pools, restores the pre-migration backup over the database file so that the original file is left untouched, and re-throws the error.
  - Detects legacy databases created via `EnsureCreated` (user tables exist without `__EFMigrationsHistory`) and halts with an actionable error rather than crashing.
- **Reason**:
  - Ensures physicians never lose clinical data during schema updates.

---

## ADR 009: Prefix Search Indexing with COLLATE NOCASE
- **Status**: Accepted
- **Decision**: Configure `NormalizedName` on `Patients` and `Medicines` (and `GenericName` on `Medicines`) with `COLLATE NOCASE`. Repositories query prefix searches via `EF.Functions.Like(p.NormalizedName, $"{cleanQuery}%")`.
- **Reason**:
  - SQLite's default collation is `BINARY`. SQLite's default case-insensitive `LIKE` operator cannot use B-Tree indexes on `BINARY` columns, causing full table scans.
  - With `COLLATE NOCASE`, SQLite's query planner automatically transforms prefix `LIKE 'query%'` into index range bounds (`NormalizedName >= 'query' AND NormalizedName < 'querz'`), executing in sub-5ms across 100,000+ records (`SEARCH Patients USING INDEX IX_Patients_NormalizedName`).

---

## ADR 010: Gated Demo Data Seeding
- **Status**: Accepted
- **Decision**: Demo data seeding runs ONLY when `DOCTORRX_DEMO=1` environment variable or `--demo-data` command-line argument is passed; never purely because of Debug compilation. In clean production mode, the database initialises with 0 doctors, 0 patients, 0 medicines, and 0 prescriptions.
- **Reason**:
  - Prevents accidental injection of fake clinical data into live medical practices.
  - Allows real physicians to set up their own profile via the profile setup form on initial launch.

---

## ADR 011: Single Instance Lock with Window Activation
- **Status**: Accepted
- **Decision**: Enforce single instance via named mutex `Local\DoctorRx_SingleInstance_Mutex`. When a secondary instance launches, it signals a named `EventWaitHandle` (`Local\DoctorRx_SingleInstance_Event`) and exits. The primary running instance responds by bringing its `MainWindow` to the foreground.
- **Reason**:
- Running multiple instances concurrently on SQLite desktop apps risks lock contention.
- Smooth physician UX: launching the shortcut again restores the already open app rather than failing silently or causing multiple conflicting windows.

---

## ADR 012: Zero Clinical Defaulting Principle & Non-Clinical Autocomplete
- **Status**: Accepted
- **Decision**: DoctorRx never suggests, defaults, pre-fills, or silently alters any clinical field (Dose, Frequency, Duration, Route, Instructions, Timing, Meal Relation). Autocomplete from the physician's medicine catalog is strictly restricted to non-clinical catalog identity information (Medicine Name, Generic Name, Form, Strength), and all pre-filled identity fields remain editable by the doctor.
- **Reason**:
  - The physician makes every clinical decision. Automated clinical guesses or defaulting introduce unacceptable clinical liability and danger of medication errors.
- **Alternatives Rejected**:
  - "Smart" auto-filling of standard adult doses (e.g., auto-filling "1 tab TDS"): Clinically unsafe; causes habituation where doctors overlook erroneous default doses.

---

## ADR 013: Draft Autosave Concurrency, Serialization, and Zombie Draft Elimination
- **Status**: Accepted
- **Decision**: 
  - Autosaves execute via debounced (2s) and safety interval (15s) timers, serialized behind a `SemaphoreSlim(1, 1)` (last write wins).
  - When finalization starts, autosave timers are stopped immediately, any in-flight background save is awaited, and composer state is marked `IsFinalized = true` to cause any future save calls to refuse to write.
  - Upon successful database commit of the finalized prescription, the draft is atomically deleted.
- **Reason**:
  - Eliminates "zombie drafts" where a lagging timer write re-creates a draft record after the prescription has already been finalized.
- **Alternatives Rejected**:
  - Unsynchronized fire-and-forget background saves: Leads to SQLite concurrency locks and race conditions during finalization.

---

## ADR 014: Untrusted Draft Patient Data and Fresh Live Database Snapshots
- **Status**: Accepted
- **Decision**: 
  - Patient demographics stored in the draft JSON payload are treated strictly as untrusted fallback hints for offline recovery.
  - On draft recovery, the application refreshes patient display information directly from the `Patients` table in SQLite.
  - On prescription finalization, doctor and patient snapshots are captured strictly from live database records, never from draft JSON or in-memory state.
- **Reason**:
  - If a patient's phone number or address was corrected in the clinic master database while a draft was open, finalization must bind to the true master record, not obsolete draft copies.
- **Alternatives Rejected**:
  - Blindly trusting draft payload snapshots: Allows stale patient demographics to be permanently stamped into immutable prescriptions.

---

## ADR 015: Medicine Form as Non-Blocking Validation Warning
- **Status**: Accepted
- **Decision**: Missing `Form` (e.g., Tablet, Syrup, Injection) is classified as a non-blocking warning rather than a fatal validation error. Blocking errors are strictly limited to: no patient selected, no medicine rows, missing medicine name, missing dose, missing frequency, or field character length violations.
- **Reason**:
  - Doctors occasionally prescribe items (e.g., compound powders, surgical dressings, special preparations) where a standard pharmaceutical form is omitted or not applicable. Doctors must not be blocked from issuing prescriptions for non-standard items.
- **Alternatives Rejected**:
  - Strict blocking validation on Form: Interrupted clinical workflow when prescribing compound preparations.

---

## ADR 016: Non-Blocking Window Close Draft Serialization
- **Status**: Accepted
- **Decision**: When `Window.Closing` is triggered with unsaved changes, the event is immediately cancelled (`e.Cancel = true`), an asynchronous confirmation dialog is presented, and upon confirmation, any pending draft is saved asynchronously (`await _draftService.SaveDraftAsync(...)`) before invoking `Application.Current.Shutdown()` or programmatic closing. No blocking calls (`.Result` or `.Wait()`) are ever permitted on the UI thread.
- **Reason**:
  - Calling synchronous blocking methods on WPF UI dispatcher threads causes deadlock risks and UI freezing.
- **Alternatives Rejected**:
  - Blocking `.Wait()` inside synchronous `Window.Closing`: Deadlock vulnerability.
  - Discarding unsaved changes on window close: Data loss.

---

## ADR 017: Medicine Usage Ranking with B-Tree Indexing
- **Status**: Accepted
- **Decision**: Maintain `UsageCount` (INTEGER) and `LastUsedAtUtc` on `Medicines`, incremented upon prescription finalization. Autocomplete queries sort matching results by `UsageCount DESC, Name ASC`. The query planner utilizes `COLLATE NOCASE` B-Tree range scans to filter matching candidates before sorting the small result set in-memory, retaining sub-5ms latency.
- **Reason**:
  - Frequently prescribed medicines naturally rise to the top of the autocomplete suggestions without requiring manual favorites management.

---

## ADR 018: Atomic Backup Containers (.drxbackup) & GFS Retention Policy
- **Status**: Accepted
- **Decision**: 
  - DoctorRx backup files use `.drxbackup` extension containing a standard ZIP bundle: `snapshot.db`, `manifest.json`, and clinic JSON settings files.
  - Snapshots are taken exclusively via the SQLite Online Backup API (`sourceConn.BackupDatabase(destConn)`) with `PRAGMA busy_timeout=5000` and exponential backoff retry to prevent `SQLITE_BUSY` or `SQLITE_LOCKED` during concurrent operations.
  - Backups write to a temporary `.tmp` file and rename atomically upon successful verification.
  - Retention implements Grandfather-Father-Son (GFS): 7 daily backups, 4 weekly backups (Sunday checkpoints), and 3 monthly backups (1st of month checkpoints). Safety backups (created before restore operations) are isolated in a separate folder with a dedicated retention limit of 5.
  - The "Protect Only Backup" rule forbids deleting the only existing backup file even if it falls outside retention windows.
- **Reason**:
  - Medical records require atomic, non-corruptible point-in-time archives that can be safely verified and restored across workstation migrations.
- **Alternatives Rejected**:
  - Raw filesystem copy of `doctorrx.db`: Dangerous in WAL mode, captures incomplete transactions or corrupts state if WAL/SHM files are uncommitted.
  - Proprietary binary backup format: Standard ZIP with JSON manifest allows manual recovery with standard tools if required.

---

## ADR 019: Safe Database Startup Integrity, Quarantine & Clean Shutdown
- **Status**: Accepted
- **Decision**:
  - On application startup, DoctorRx validates database path safety: network/UNC paths are blocked to prevent SQLite network filesystem corruption.
  - Executes `PRAGMA quick_check;` and `PRAGMA foreign_key_check;`. If severe corruption is detected, the corrupt database is safely quarantined by renaming to `doctorrx.corrupt-<timestamp>.db` rather than silently overwritten or ignored.
  - On application shutdown (via `App.OnExit`, `OnSessionEnding`, or `MainWindow.OnClosing`), DoctorRx checkpoints and flattens the write-ahead log using `PRAGMA wal_checkpoint(TRUNCATE);` and runs auto-backup if enabled.
  - Safe restore engine verifies SHA-256 and schema compatibility, creates a safety backup, stages extracted files in a temporary swap directory, and performs an atomic directory swap with full rollback capability.
- **Reason**:
  - Zero tolerance for clinical data loss, silent corruption, or dangling uncommitted WAL entries on shutdown.

---

## ADR 020: Single Active Doctor Invariant and Lowest-ID Migration Deduplication
- **Status**: Accepted
- **Decision**: 
  - The database enforces a strictly unique active doctor invariant via a partial unique index: `CREATE UNIQUE INDEX IX_Doctors_SingleActive ON Doctors (IsActive) WHERE IsActive = 1;`.
  - When migrating existing databases with multiple active doctors, migration `20261010100334_AddPrescriptionIsSealed` executes `UPDATE Doctors SET IsActive = 0 WHERE IsActive = 1 AND Id NOT IN (SELECT Id FROM Doctors WHERE IsActive = 1 ORDER BY Id ASC LIMIT 1);`.
  - This preserves the lowest-Id active doctor (`ORDER BY Id ASC LIMIT 1`) to maintain deterministic continuity with the primary profile that the desktop application initially displayed and used.
- **Reason**:
  - Medical practices using DoctorRx operate with a single active primary prescriber per installation. Historical rows must never cause ambiguity or multiple concurrent active states. Preserving the lowest-Id record guarantees that the original established doctor record remains active.



