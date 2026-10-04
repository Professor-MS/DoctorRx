# DoctorRx Architectural Specification

## 1. Domain Entities & Database Schema

The database is built on SQLite via Entity Framework Core 10. Indexes are created on frequently queried columns for performance at scale (e.g. 50,000+ patient records).

### Patients Table (`Patients`)
- `Id` (INTEGER, PK, Auto-increment)
- `Name` (NVARCHAR(150), Indexed)
- `DateOfBirth` (DATETIME, Nullable)
- `Age` (INTEGER, Nullable)
- `Gender` (INTEGER - Enum: 0=NotSpecified, 1=Male, 2=Female, 3=Other)
- `Phone` (NVARCHAR(30), Indexed)
- `Address` (NVARCHAR(300))
- `MedicalHistoryNotes` (NVARCHAR(1000))
- `KnownAllergies` (NVARCHAR(500))
- `CreatedAtUtc` (DATETIME)
- `UpdatedAtUtc` (DATETIME, Nullable)

### Doctors Table (`Doctors`)
- `Id` (INTEGER, PK)
- `Name` (NVARCHAR(150))
- `Qualification` (NVARCHAR(150))
- `RegistrationNumber` (NVARCHAR(50))
- `Specialization` (NVARCHAR(150))
- `Phone` (NVARCHAR(30))
- `Email` (NVARCHAR(100))
- `ClinicName` (NVARCHAR(200))
- `ClinicAddress` (NVARCHAR(300))
- `ClinicPhone` (NVARCHAR(30))
- `HeaderText` (NVARCHAR(500))
- `FooterText` (NVARCHAR(500))
- `IsActive` (BOOLEAN)

### Medicines Table (`Medicines`)
- `Id` (INTEGER, PK)
- `Name` (NVARCHAR(150), Indexed)
- `GenericName` (NVARCHAR(150), Indexed)
- `Form` (NVARCHAR(50) - Tablet, Syrup, Injection, Capsule, Drops, Ointment, etc.)
- `Strength` (NVARCHAR(50) - e.g. 500 mg, 120 mg/5 ml, 10 ml)
- `DefaultDose` (NVARCHAR(50))
- `DefaultFrequency` (NVARCHAR(50))
- `DefaultRoute` (NVARCHAR(50))
- `DefaultInstructions` (NVARCHAR(500))
- `IsActive` (BOOLEAN)

### Prescriptions Table (`Prescriptions`)
- `Id` (INTEGER, PK)
- `PrescriptionNumber` (NVARCHAR(50), Unique Index - format `RX-YYYYMMDD-####`)
- `PatientId` (INTEGER, FK -> Patients, Restrict Delete)
- `DoctorId` (INTEGER, FK -> Doctors, Restrict Delete)
- `PrescriptionDate` (DATETIME, Indexed)
- `ChiefComplaints` (NVARCHAR(1000))
- `BloodPressure` (NVARCHAR(20))
- `PulseRate` (NVARCHAR(20))
- `Temperature` (NVARCHAR(20))
- `WeightKg` (NVARCHAR(20))
- `ClinicalNotes` (NVARCHAR(2000))
- `GeneralAdvice` (NVARCHAR(2000))
- `FollowUpDate` (DATETIME, Nullable)
- `Status` (INTEGER - Enum: 0=Draft, 1=Finalized, 2=Cancelled)
- `CreatedAtUtc` (DATETIME)
- `UpdatedAtUtc` (DATETIME, Nullable)

### PrescriptionMedicines Table (`PrescriptionMedicines`)
- `Id` (INTEGER, PK)
- `PrescriptionId` (INTEGER, FK -> Prescriptions, Cascade Delete)
- `MedicineId` (INTEGER, Nullable, FK -> Medicines, Set Null on Delete)
- `MedicineName` (NVARCHAR(150) - Immutable snapshot)
- `GenericName` (NVARCHAR(150) - Immutable snapshot)
- `Form` (NVARCHAR(50) - Immutable snapshot)
- `Strength` (NVARCHAR(50) - Immutable snapshot)
- `Dose` (NVARCHAR(50) - e.g. 1 tab, 5 ml)
- `Frequency` (NVARCHAR(50) - e.g. TDS, BD, 1-0-1)
- `Timing` (NVARCHAR(100))
- `MealRelation` (INTEGER - Enum: BeforeMeal, AfterMeal, WithMeal, EmptyStomach, etc.)
- `CustomMealRelationText` (NVARCHAR(100))
- `Route` (NVARCHAR(50) - Oral, Topical, IV, etc.)
- `Duration` (NVARCHAR(50) - e.g. 5 days, 1 month)
- `Instructions` (NVARCHAR(500))
- `SortOrder` (INTEGER)

---

## 2. Phase 2 Roadmap & Next Steps

1. **Prescription Composer UI**:
   - Multi-step prescription builder.
   - Dynamic medicine row addition, editing, reordering, and removal.
   - Predefined and custom dosage / frequency / meal relation dropdowns.
2. **Printing & PDF Generation**:
   - Print preview engine using WPF FlowDocument.
   - Direct printing to Windows print subsystem.
   - Microsoft Print to PDF integration.
   - Customizable page sizes (A4, A5, Custom prescription pad with preprinted headers).
3. **Prescription Archive & History**:
   - Patient-centric prescription lookup.
   - Duplicate previous prescription into a new draft.
4. **Settings & Backup**:
   - Doctor credentials & clinic information configuration UI.
   - Local database backup & restore.
