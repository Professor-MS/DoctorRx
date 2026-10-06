# DoctorRx

DoctorRx is a professional, offline-first Windows desktop prescription management application engineered for physicians and clinic practices.

DoctorRx provides a structured, responsive, and readable prescription creation workflow based entirely on the doctor's clinical decisions.
> **Medical Principle**: DoctorRx does **not** diagnose patients, calculate medical dosages, or make automated clinical decisions. The physician makes every clinical choice; DoctorRx only records it. Zero default dosages, formulations, frequencies, or durations are ever injected or assumed.

---

## Architecture Overview

DoctorRx is built with **.NET 10 LTS**, **C#**, **WPF**, and **Clean Architecture** with strict layer separation:

```
DoctorRx
├── DoctorRx.sln                  # Visual Studio 2026 solution
├── DoctorRx.slnx                 # Modern XML-based solution format
├── Directory.Build.props         # Global versioning (0.1.0)
├── src
│   ├── DoctorRx.Domain           # Entities, Enums, Interfaces (Zero dependencies)
│   ├── DoctorRx.Application      # Use cases, DTOs, Service Interfaces, Logic
│   ├── DoctorRx.Infrastructure   # EF Core 10, SQLite Migrator, Anti-tamper Triggers, Repositories
│   └── DoctorRx.Presentation     # WPF (MVVM), CommunityToolkit, Styles, Views, Serilog
├── tests
│   └── DoctorRx.Tests            # 100% Real SQLite integration tests & 100k benchmark
└── docs                          # Architecture specifications and ADRs (001-011)
```

### Key Architectural Standards

1. **Immutable Medicine Snapshots & SQLite Anti-Tamper Triggers**:
   Finalized prescriptions preserve exact frozen snapshots of the physician credentials, patient demographics, and prescribed medicines at the moment of finalization. Triggers in the SQLite storage engine prohibit updates to clinical metadata, prevent item deletions, and restrict status transitions strictly to `Cancelled` or `Superseded`.

2. **Atomic Gap-Free Numbering via `BEGIN IMMEDIATE`**:
   Prescription sequence numbers are allocated within the exact same database write transaction as the entity insertion. On transaction rollback, the sequence counter rolls back, guaranteeing zero burned numbers. On `SQLITE_BUSY`, the entire transaction retries with exponential backoff and jitter.

3. **Offline-First & Local Storage**:
   Data resides under `%LOCALAPPDATA%\DoctorRx\Data\doctorrx.db` with SQLite durability pragmas enforced on connection open: `WAL`, `foreign_keys=ON`, `busy_timeout=5000`, `synchronous=FULL`.

4. **Indexed Keystroke Search with `COLLATE NOCASE`**:
   Patient and medicine normalized names use `COLLATE NOCASE`, allowing SQLite prefix searches (`LIKE 'query%'`) to execute via indexed B-Tree range scans in sub-5ms across 100,000+ records.

5. **Safe Migration, Automated Backups, and Legacy Guard**:
   `DatabaseMigrator` creates pre-migration backups using SQLite Online Backup API only when pending migrations exist, keeps the last 5 backups, provides automatic rollback on migration failure, and halts gracefully if a legacy `EnsureCreated` database is detected.

6. **Production Logging & Privacy**:
   Serilog writes rolling daily logs to `%LOCALAPPDATA%\DoctorRx\Logs\doctorrx-.log`. Absolutely zero patient PII (names, phone digits, addresses, clinical notes, allergies) is ever logged. Unhandled exceptions display an 8-character hex reference ID for technical support without exposing raw stack traces to the clinic user.

7. **Single Instance & PerMonitorV2 DPI Awareness**:
   Enforced via named mutex `Local\DoctorRx_SingleInstance_Mutex` and named `EventWaitHandle`. Launching a secondary shortcut automatically activates and brings the running window to the foreground.

---

## Prerequisites

- Windows 10 / 11 (x64)
- **.NET 10 SDK** (Installed: .NET 10.0.401+)
- **Visual Studio 2026** (or Visual Studio 2022 v17.12+ with .NET 10 workload)

---

## How to Run

Build the solution:
```bash
dotnet build DoctorRx.sln
```

Run the complete SQLite test suite (30 tests, including 100k benchmark):
```bash
dotnet test DoctorRx.sln
```

Launch the WPF application in clean production mode:
```bash
dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
```

Launch with demo seed data enabled:
```bash
dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj -- --demo-data
# OR set environment variable: DOCTORRX_DEMO=1
```
