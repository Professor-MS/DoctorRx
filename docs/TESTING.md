# DoctorRx Testing Guide

This document outlines the testing strategy, procedures, and benchmark protocols for DoctorRx.

---

## 1. Automated Test Suite

DoctorRx features a comprehensive automated test suite covering domain rules, service logic, EF Core 10 migrations, SQLite storage engines, triggers, query execution plans, and WPF ViewModels.

### Running All Unit and Integration Tests
To run all automated tests (excluding long-running bulk benchmarks):
```bash
dotnet test tests/DoctorRx.Tests/DoctorRx.Tests.csproj --filter "FullyQualifiedName!~Benchmark"
```

### Running the Full Suite (Including 500k Benchmarks)
```bash
dotnet test DoctorRx.sln
```

### Key Automated Test Categories
- **`AntiTamperSchemaDrivenTests`**: Schema-driven test that dynamically queries `PRAGMA table_info('Prescriptions')` and asserts that every non-transition column (including `FollowUpText`, clinical notes, vitals, patient/doctor snapshots) aborts any direct `UPDATE` on a finalized prescription via SQLite triggers.
- **`SearchNormalizerTests`**: Validates Urdu/Arabic normalization, diacritic stripping, tatweel removal, zero-width joiner/non-joiner removal, Arabic-Indic digit normalization, and mixed English+Urdu text strings.
- **`SearchQueryPlanTests`**: Validates that SQLite query planner uses the `COLLATE NOCASE` B-Tree indexes for prefix searches (`SEARCH Patients USING INDEX IX_Patients_NormalizedName` and `SEARCH Medicines USING INDEX IX_Medicines_NormalizedName`) without table scans, even with `ORDER BY UsageCount DESC`.
- **`ModelSnapshotTests`**: Asserts that EF Core model snapshot matches the compiled entity model and fails if developers modify entities without running `dotnet ef migrations add`.
- **`DraftServiceTests` & `NewPrescriptionViewModelTests`**: Tests autosave debouncing, draft recovery, live database patient refresh, corrupt payload handling, 30-day age flag, and zombie draft race conditions.
- **`SQLiteBenchmarkTests`**: Generates and queries 500,000 prescriptions and 1,500,000 medicine items, verifying history load, prescription detail retrieval, and finalization timings.

---

## 2. Manual Crash Recovery Test (Kill-the-Process)

The autosave and draft recovery system is designed to survive sudden hardware failure, power loss, or operating system termination without losing unsaved clinical work.

### Step-by-Step Crash Test Procedure

1. **Launch DoctorRx**:
   ```bash
   dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
   ```
2. **Begin Composing a Prescription**:
   - Press `Ctrl+N` or click **"New Prescription"** from the navigation bar.
   - In the Patient section, select or create a patient (e.g., "Muhammad Ali").
   - Record clinical vitals: BP `120/80`, Pulse `72`, Temp `98.6°F`, Weight `70 kg`.
3. **Add Prescribed Medicines**:
   - In the medicine editor, search or type `Amoxicillin 500 mg`.
   - Fill in: Dose `1 capsule`, Frequency `TDS`, Duration `5 days`, Route `Oral`, Instructions `After meals with water`.
   - Press `Enter` (or click "Add Medicine"). Verify the medicine is added to the prescription table and focus immediately returns to the medicine name input box.
   - Add a second medicine: `Paracetamol 500 mg`, Dose `1 tab`, Frequency `SOS`, Duration `3 days`, Route `Oral`, Instructions `For fever`.
   - Add Chief Complaints: `High fever and throat pain for 3 days`.
   - Add General Advice: `Drink plenty of warm liquids. Rest.`.
4. **Observe Autosave Indicator**:
   - Within 2 seconds of the last keystroke, observe the status text in the header:
     `"Draft saved at HH:mm:ss"`.
   - The prescription is now safely persisted in SQLite (`Drafts` table).
5. **Abruptly Terminate the Process (Simulated Crash)**:
   - Without clicking Save, Finalize, or closing the window cleanly, open PowerShell/Command Prompt and execute:
     ```powershell
     taskkill /F /IM DoctorRx.Presentation.exe
     ```
   - The application process is instantly terminated by Windows kernel signal.
6. **Relaunch DoctorRx**:
   ```bash
   dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
   ```
7. **Verify Startup Recovery Dialog**:
   - On application startup, the recovery system checks for unfinalized drafts.
   - A recovery dialog appears:
     > *"Unfinalized Prescription Draft Found: You have an unsaved prescription draft for patient Muhammad Ali from [Time]. Would you like to resume editing it now?"*
   - Click **"Resume Draft"**.
8. **Verify Restored Clinical State**:
   - The New Prescription screen opens populated with all previously entered data:
     - Patient selected: Muhammad Ali.
     - Vitals: BP `120/80`, Pulse `72`, Temp `98.6°F`, Weight `70 kg`.
     - Prescribed medicines: Both items intact with full doses, frequencies, routes, and instructions.
     - Advice and clinical notes: Fully restored.
9. **Verify Fresh Database Patient Refresh**:
   - The recovery mechanism queries the live `Patients` database table rather than relying on stale cached payload fields.
   - Any updates made to the patient's record in the database while the draft was closed are accurately reflected.
10. **Finalize and Clean Up**:
    - Click **"Finalize Prescription"** (`Ctrl+Enter`).
    - Verify the prescription is finalized successfully.
    - Check the dashboard: the draft has been atomically removed from the active drafts list.

---

## 3. Keyboard Navigation and Accessibility Testing

DoctorRx is designed for efficient keyboard-driven clinical entry:

| Key Binding | Target / Action | Behavior |
|:---|:---|:---|
| **`Ctrl+N`** | Global | Navigate directly to New Prescription screen. |
| **`Ctrl+S`** | Global / Composer | Manually trigger immediate draft save. |
| **`F1`** | Global | Open / Close Keyboard Shortcuts & Guide overlay. |
| **`Enter`** | Medicine Editor | Adds current medicine to prescription table and refocuses Medicine Name search box. |
| **`Esc`** | Medicine Editor | Cancels current inline edit and refocuses Medicine Name search box. |
| **`Tab`** | All Form Fields | Natural, logical left-to-right tab order across all clinical fields. |

### Verifying Screen Reader Accessibility:
- Every interactive element and input control specifies an `AutomationProperties.Name` attribute (e.g. `AutomationProperties.Name="Medicine Name Search"`).
- Test with Windows Narrator (`Win + Ctrl + Enter`) to verify audible field labels.

---

## 4. Gate 5A Backup, Restore & Disaster Recovery Verification Protocols

### Protocol A: Manual Backup Creation & Verification
1. Launch DoctorRx:
   ```bash
   dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
   ```
2. Navigate to **Settings** (`Clinic & Application Settings`) from the left sidebar or the gear icon.
3. On the **Database & Backups** tab, click **"Back Up Now"**.
4. Confirm progress indicator updates and notification appears: *"Backup Created Successfully"*.
5. In the **Available Backup Archives** card, click **"Verify"** next to the newly created archive.
6. Verify status updates to a green badge: *"Verified"* with integrity pragmas passed and zero errors.

### Protocol B: Dashboard Status & One-Click Backup
1. On the **Dashboard**, locate the **Database Backup & Health Status Bar**.
2. Notice the live status:
   - If no backup in 24 hours: Badge displays *"Backup Recommended"* (amber).
   - Click the **"Back Up Now"** button directly on the dashboard status card.
   - Verify that upon completion, the badge immediately updates to *"Protected"* (green) with the timestamp.

### Protocol C: Safe Restore with Automatic Safety Snapshot
1. In **Settings -> Database & Backups**, locate an archive and click **"Restore"** (or click **"Restore External..."**).
2. A confirmation dialog warns: *"Restoring will replace the active database with data from the backup archive. A safety snapshot will be created before restoring."*
3. Click **"Proceed with Restore"**.
4. Observe the restore pipeline:
   - Checksum and schema versions are validated.
   - A pre-restore safety backup is automatically stored in `%LOCALAPPDATA%\DoctorRx\Backups\Safety\`.
   - Files are staged and atomically swapped into `%LOCALAPPDATA%\DoctorRx\Data\`.
   - Notification confirms successful restore.

### Protocol D: Database Corruption Detection & Quarantine
1. Terminate the app.
2. In a test environment, simulate file header corruption on a test database:
   ```powershell
   [IO.File]::WriteAllBytes("$env:LOCALAPPDATA\DoctorRx\Data\doctorrx.db", (Get-Content "$env:LOCALAPPDATA\DoctorRx\Data\doctorrx.db" -Encoding Byte | Select-Object -Skip 100))
   ```
3. Launch the application.
4. Verify that DoctorRx halts gracefully with a clear startup message indicating database corruption.
5. Inspect the data directory: verify the corrupted database was moved to `doctorrx.corrupt-<timestamp>.db` to preserve forensic evidence rather than destroyed.

### Protocol E: Network / UNC Path Storage Block
1. Attempt to launch DoctorRx with a network UNC path:
   ```powershell
   $env:DOCTORRX_DATA_DIR = "\\192.168.1.100\SharedClinic\DoctorRx"
   dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
   ```
2. Observe startup halt with actionable message:
   > *"Network or UNC shared folders are not supported for SQLite database storage because network file locks can cause silent corruption."*
3. Clear the override:
   ```powershell
   $env:DOCTORRX_DATA_DIR = $null
   ```

