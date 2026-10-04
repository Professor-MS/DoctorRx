# DoctorRx

DoctorRx is a professional, offline-first Windows desktop prescription management application engineered for physicians and clinic practices.

DoctorRx provides a structured, responsive, and readable prescription creation workflow based entirely on the doctor's clinical decisions.
> **Medical Disclaimer**: DoctorRx does **not** diagnose patients, calculate medical dosages, or make automated clinical decisions. It is a physician productivity and medical records system.

---

## Architecture Overview

DoctorRx is built with **.NET 10 LTS**, **C#**, **WPF**, and **Clean Architecture** with strict layer separation:

```
DoctorRx
├── DoctorRx.sln                  # Visual Studio 2026 solution
├── DoctorRx.slnx                 # Modern XML-based solution format
├── src
│   ├── DoctorRx.Domain           # Entities, Enums, Interfaces (Zero dependencies)
│   ├── DoctorRx.Application      # Use cases, DTOs, Service Interfaces, Logic
│   ├── DoctorRx.Infrastructure   # EF Core, SQLite DbContext, Repositories, Seeding
│   └── DoctorRx.Presentation     # WPF (MVVM), CommunityToolkit, Styles, Views
├── tests
│   └── DoctorRx.Tests            # xUnit tests, EF Core In-Memory verification
└── docs                          # Architectural diagrams and roadmap specifications
```

### Key Architectural Rules

1. **Immutable Medicine Snapshots**:
   A prescription preserves the exact medicine information (Brand Name, Generic Name, Form, Strength, Dose, Frequency, Route, Duration, Instructions) prescribed at that point in time. If a master medicine catalog item is edited or retired later, **historical prescriptions remain completely unchanged**.

2. **Clean MVVM & Dependency Injection**:
   Views bind cleanly to ViewModels. ViewModels access Application Service interfaces (`IPatientService`, `IPrescriptionService`, `IDashboardService`). Services use the Unit of Work and Repositories. Views never communicate directly with SQLite or Entity Framework.

3. **Offline-First & Local Storage**:
   Data is stored securely in SQLite with Write-Ahead Logging (WAL) enabled in `%LOCALAPPDATA%\DoctorRx\doctorrx.db`.

4. **Global Resilience & Error Handling**:
   Unhandled exceptions are intercepted globally (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) to prevent unhandled crashes and provide clear user messages without technical stack traces.

---

## Prerequisites

- Windows 10 / 11 (x64)
- **.NET 10 SDK** (Installed: .NET 10.0.401+)
- **Visual Studio 2026** (or Visual Studio 2022 v17.12+ with .NET 10 workload)

---

## How to Open in Visual Studio 2026

1. Launch Visual Studio 2026.
2. Select **Open a project or solution**.
3. Navigate to:
   - `D:\DoctorRx\DoctorRx.sln` (or `C:\Users\Professor\Desktop\DoctorRx Project\DoctorRx.sln`)
4. Set `DoctorRx.Presentation` as the **Startup Project** (it is set by default).
5. Press **F5** (or click the green **Start** button).

The solution will automatically:
- Restore any missing NuGet packages.
- Build all 5 projects.
- Initialize the local SQLite database schema.
- Seed starter doctor profile, essential medicine catalog, and sample patient records.
- Display the DoctorRx Clinic Overview dashboard.

---

## Running from Command Line

Build the solution:
```bash
dotnet build DoctorRx.sln
```

Run all unit tests:
```bash
dotnet test DoctorRx.sln
```

Launch the WPF application:
```bash
dotnet run --project src/DoctorRx.Presentation/DoctorRx.Presentation.csproj
```

---

## Features Implemented in Phase 1

- [x] Complete Clean Architecture solution structure.
- [x] Full Domain models (`Patient`, `Doctor`, `Medicine`, `Prescription`, `PrescriptionMedicine`).
- [x] EF Core 10 SQLite database context with schema configuration, indexes, and migrations compatibility.
- [x] Database initial seeding (Default doctor profile, starter medicine catalog, sample patients, and initial prescription).
- [x] Modern, professional WPF user interface with a custom medical slate/teal design system.
- [x] Responsive Clinic Dashboard with key performance indicators (Total Patients, Today's Prescriptions, All-time Prescriptions, Catalog size, Recent Prescriptions, Recent Patients).
- [x] Interactive Patient Management section:
  - Live patient search and filtering across names and phone numbers.
  - Patient data grid with MRN, age, gender badges, contact, and address.
  - Slide-over registration drawer for registering and updating patient records with real-time validation.
- [x] MVVM Navigation rail linking to Dashboard, New Prescription, Patients, History, Medicines, and Settings.
- [x] Unit test suite covering domain snapshot immutability and application service validation.
