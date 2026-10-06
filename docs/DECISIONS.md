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

