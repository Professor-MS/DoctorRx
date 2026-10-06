# DoctorRx Architectural Specification

## 1. Domain Entities & Database Schema

The database is built on SQLite via Entity Framework Core 10 with Write-Ahead Logging (`WAL`), `foreign_keys=ON`, `busy_timeout=5000`, and `synchronous=FULL` (ADR 001). Indexes are created on frequently queried columns for performance at scale (e.g. 100,000+ patient records).

### Patients Table (`Patients`)
- `Id` (INTEGER, PK, Auto-increment)
- `RecordNumber` (TEXT(50), Unique Index - format `P-######`)
- `Name` (TEXT(150))
- `NormalizedName` (TEXT(150) COLLATE NOCASE, Indexed for prefix search)
- `DateOfBirth` (TEXT, Nullable, DateOnly format `yyyy-MM-dd`)
- `Age` (INTEGER, Nullable)
- `AgeRecordedDate` (TEXT, Nullable, DateOnly format `yyyy-MM-dd`)
- `Gender` (INTEGER - Enum: 0=NotSpecified, 1=Male, 2=Female, 3=Other)
- `Phone` (TEXT(30))
- `PhoneDigits` (TEXT(30), Indexed for numeric search)
- `Address` (TEXT(300))
- `MedicalHistoryNotes` (TEXT(1000))
- `KnownAllergies` (TEXT(500))
- `IsArchived` (INTEGER, Default 0, Indexed)
- `ArchivedAtUtc` (TEXT, Nullable)
- `LastVisitDate` (TEXT, Nullable, DateOnly format - automatically tracked on prescription finalize and recalculated on cancel)
- `CreatedAtUtc` (TEXT)
- `UpdatedAtUtc` (TEXT, Nullable)

### Doctors Table (`Doctors`)
- `Id` (INTEGER, PK, Auto-increment)
- `Name` (TEXT(150))
- `Qualification` (TEXT(150))
- `RegistrationNumber` (TEXT(50))
- `Specialization` (TEXT(150))
- `Phone` (TEXT(30))
- `Email` (TEXT(100))
- `ClinicName` (TEXT(200))
- `ClinicAddress` (TEXT(300))
- `ClinicPhone` (TEXT(30))
- `HeaderText` (TEXT(500))
- `FooterText` (TEXT(500))
- `IsActive` (INTEGER, Default 1)
- `CreatedAtUtc` (TEXT)
- `UpdatedAtUtc` (TEXT, Nullable)

### Medicines Table (`Medicines`)
- `Id` (INTEGER, PK, Auto-increment)
- `Name` (TEXT(150), Indexed)
- `NormalizedName` (TEXT(150) COLLATE NOCASE, Indexed for prefix search)
- `GenericName` (TEXT(150) COLLATE NOCASE, Indexed for prefix search)
- `Form` (TEXT(50) - Tablet, Syrup, Injection, Capsule, Drops, Ointment, etc. Required, zero defaults)
- `Strength` (TEXT(50) - e.g. 500 mg, 120 mg/5 ml)
- `IsActive` (INTEGER, Default 1)
- `CreatedAtUtc` (TEXT)
- `UpdatedAtUtc` (TEXT, Nullable)

*(Core product principle: DoctorRx never stores default dose, frequency, route, or instructions in the catalog).*

### Prescriptions Table (`Prescriptions`)
- `Id` (INTEGER, PK, Auto-increment)
- `PrescriptionNumber` (TEXT(50), Unique Index - format `RX-YYYYMMDD-####` or `RX-YYYYMMDD-####-A#`)
- `PatientId` (INTEGER, FK -> Patients, Restrict Delete)
- `DoctorId` (INTEGER, FK -> Doctors, Restrict Delete)
- `PrescriptionDate` (TEXT, DateOnly, Indexed)
- `ParentPrescriptionId` (INTEGER, Nullable, FK -> Prescriptions)
- `AmendmentNumber` (INTEGER, Default 0)
- `Version` (INTEGER, Concurrency Token)
- `Doctor_Name` ... `Doctor_ClinicName` (Owned Doctor Snapshot at time of finalization)
- `Patient_Name`, `Patient_Gender`, `Patient_AgeText` (Owned Patient Snapshot at time of finalization)
- `ChiefComplaints` (TEXT(1000))
- `BloodPressure` (TEXT(20))
- `PulseRate` (TEXT(20))
- `Temperature` (TEXT(20))
- `WeightKg` (TEXT(20))
- `ClinicalNotes` (TEXT(2000))
- `GeneralAdvice` (TEXT(2000))
- `FollowUpDate` (TEXT, Nullable, DateOnly)
- `Status` (INTEGER - Enum: 1=Finalized, 2=Cancelled, 3=Superseded)
- `FinalizedAtUtc` (TEXT)
- `CancelledAtUtc` (TEXT, Nullable)
- `CancellationReason` (TEXT(500), Nullable)
- `CreatedAtUtc` (TEXT)
- `UpdatedAtUtc` (TEXT, Nullable)

### PrescriptionMedicines Table (`PrescriptionMedicines`)
- `Id` (INTEGER, PK, Auto-increment)
- `PrescriptionId` (INTEGER, FK -> Prescriptions, Cascade Delete)
- `MedicineId` (INTEGER, FK -> Medicines, `ON DELETE RESTRICT`)
- `MedicineName` (TEXT(150) - Immutable snapshot)
- `GenericName` (TEXT(150), Nullable - Immutable snapshot)
- `Form` (TEXT(50) - Immutable snapshot)
- `Strength` (TEXT(50) - Immutable snapshot)
- `Dose` (TEXT(50) - e.g. 1 tab, 5 ml)
- `Frequency` (TEXT(50) - e.g. TDS, BD, 1-0-1)
- `Timing` (TEXT(100), Nullable)
- `MealRelation` (INTEGER - Enum: BeforeMeal, AfterMeal, WithMeal, EmptyStomach, etc.)
- `CustomMealRelationText` (TEXT(100), Nullable)
- `WithWhat` (TEXT(100), Nullable)
- `Route` (TEXT(50))
- `Duration` (TEXT(50))
- `Instructions` (TEXT(500), Nullable)
- `SortOrder` (INTEGER)

### NumberSequences Table (`NumberSequences`)
- `SequenceKey` (TEXT(50), PK)
- `CurrentValue` (INTEGER)
- `UpdatedAtUtc` (TEXT)

---

## 2. Anti-Tamper SQLite Triggers

Immutability and transition state integrity are enforced at the SQLite engine level:
1. `trg_prevent_prescription_tamper`: Prohibits mutation of finalized clinical metadata and doctor/patient snapshots. Allows only valid state transitions (`Finalized` -> `Cancelled` or `Superseded`). Blocks any edits once a prescription reaches terminal status (`Cancelled` or `Superseded`).
2. `trg_prevent_prescription_delete`: Prohibits deleting prescription records.
3. `trg_prevent_prescription_medicine_delete`: Prohibits deleting prescribed medicine items.
4. `trg_prevent_prescription_medicine_update`: Prohibits altering prescribed medicine rows.
5. `trg_prevent_prescription_medicine_insert_after_terminal`: Prohibits adding medicine items to cancelled or superseded prescriptions.

---

## 3. Atomic Sequence Numbering & Resilience

- Sequence numbers are allocated within the exact same SQLite write transaction as the prescription record using `BEGIN IMMEDIATE` semantics via `IUnitOfWork.BeginWriteTransactionAsync()`.
- Rolling back the transaction reverts the sequence counter, ensuring zero gaps on failed saves.
- Transient lock contention (`SQLITE_BUSY`) triggers a full-transaction retry loop with exponential jitter (up to 5 attempts).
