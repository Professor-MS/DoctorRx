using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Data;

public class DemoDataSeeder : IDemoDataSeeder
{
    private readonly IDbContextFactory<DoctorRxDbContext> _contextFactory;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        IDbContextFactory<DoctorRxDbContext> contextFactory,
        ILogger<DemoDataSeeder> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Seed Doctor if none exists
        if (!await context.Doctors.AnyAsync(cancellationToken))
        {
            var doctor = new Doctor
            {
                Name = "Dr. Muhammad Tariq",
                Qualification = "MBBS, FCPS (Internal Medicine)",
                RegistrationNumber = "48291-P",
                Specialization = "Consultant Physician",
                Phone = "+92 300 1234567",
                Email = "dr.tariq@doctorrx.local",
                ClinicName = "Al-Shifa Family Healthcare Clinic",
                ClinicAddress = "Suite 4B, Blue Area Medical Plaza, Islamabad",
                ClinicPhone = "+92 51 2890123",
                HeaderText = "AL-SHIFA HEALTHCARE CLINIC • PH: +92 51 2890123",
                FooterText = "Not valid for medico-legal purposes • Emergency: Visit nearest hospital immediately",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await context.Doctors.AddAsync(doctor, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo doctor profile.");
        }

        // 2. Seed Medicines catalog if empty
        if (!await context.Medicines.AnyAsync(cancellationToken))
        {
            var medicines = new List<Medicine>
            {
                new() { Name = "Panadol", GenericName = "Paracetamol", Form = "Tablet", Strength = "500 mg", NormalizedName = "panadol", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Augmentin", GenericName = "Amoxicillin + Clavulanic Acid", Form = "Tablet", Strength = "625 mg", NormalizedName = "augmentin", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Flagyl", GenericName = "Metronidazole", Form = "Tablet", Strength = "400 mg", NormalizedName = "flagyl", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Risek", GenericName = "Omeprazole", Form = "Capsule", Strength = "20 mg", NormalizedName = "risek", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Brufen", GenericName = "Ibuprofen", Form = "Tablet", Strength = "400 mg", NormalizedName = "brufen", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Ponstan", GenericName = "Mefenamic Acid", Form = "Tablet", Strength = "500 mg", NormalizedName = "ponstan", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Glucophage", GenericName = "Metformin HCl", Form = "Tablet", Strength = "500 mg", NormalizedName = "glucophage", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Arinac Forte", GenericName = "Ibuprofen + Pseudoephedrine", Form = "Tablet", Strength = "400/60 mg", NormalizedName = "arinac forte", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Hydryllin", GenericName = "Aminophylline Compound", Form = "Syrup", Strength = "120 ml", NormalizedName = "hydryllin", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Ciproxin", GenericName = "Ciprofloxacin", Form = "Tablet", Strength = "500 mg", NormalizedName = "ciproxin", CreatedAtUtc = DateTime.UtcNow }
            };

            await context.Medicines.AddRangeAsync(medicines, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo medicine catalog.");
        }

        // 3. Seed Sample Patients if empty
        if (!await context.Patients.AnyAsync(cancellationToken))
        {
            var patients = new List<Patient>
            {
                new()
                {
                    RecordNumber = "P-000001",
                    Name = "Abdul Rehman",
                    NormalizedName = "abdul rehman",
                    DateOfBirth = new DateOnly(1982, 5, 14),
                    Age = 44,
                    Gender = Gender.Male,
                    Phone = "+92 333 4567890",
                    PhoneDigits = "923334567890",
                    Address = "House 12, Street 7, Sector F-10/2, Islamabad",
                    MedicalHistoryNotes = "Hypertension diagnosed 2022. Well controlled.",
                    KnownAllergies = "Sulfa drugs",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-20)
                },
                new()
                {
                    RecordNumber = "P-000002",
                    Name = "Fatima Bibi",
                    NormalizedName = "fatima bibi",
                    DateOfBirth = new DateOnly(1995, 11, 23),
                    Age = 30,
                    Gender = Gender.Female,
                    Phone = "+92 301 9876543",
                    PhoneDigits = "923019876543",
                    Address = "Apartment 302, Silver Oaks, F-10, Islamabad",
                    MedicalHistoryNotes = "No major chronic illnesses reported.",
                    KnownAllergies = "Penicillin (rash)",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    RecordNumber = "P-000003",
                    Name = "Muhammad Usman",
                    NormalizedName = "muhammad usman",
                    DateOfBirth = new DateOnly(2012, 3, 8),
                    Age = 14,
                    Gender = Gender.Male,
                    Phone = "+92 321 5551234",
                    PhoneDigits = "923215551234",
                    Address = "Sector G-9/1, Islamabad",
                    MedicalHistoryNotes = "Occasional seasonal allergic rhinitis.",
                    KnownAllergies = "None reported",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            };

            await context.Patients.AddRangeAsync(patients, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo patients.");

            // Create a sample prescription for Abdul Rehman
            var doctor = await context.Doctors.FirstAsync(cancellationToken);
            var samplePatient = patients[0];

            var rx = Prescription.CreateFinalized(
                prescriptionNumber: $"RX-{DateTime.Today:yyyyMMdd}-0001",
                patientId: samplePatient.Id,
                doctorId: doctor.Id,
                prescriptionDate: DateOnly.FromDateTime(DateTime.Today),
                doctorSnapshot: doctor.ToSnapshot(),
                patientSnapshot: samplePatient.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
                finalizedAtUtc: DateTime.UtcNow,
                chiefComplaints: "Fever and mild throat soreness for 2 days",
                bloodPressure: "120/80",
                pulseRate: "78 bpm",
                temperature: "100.4 F",
                weightKg: "76 kg",
                clinicalNotes: "Pharyngeal erythema observed. Chest clear to auscultation.",
                generalAdvice: "Drink warm fluids, salt water gargles 3 times daily. Complete rest.",
                followUpDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5))
            );

            rx.AddMedicine(new PrescriptionMedicine
            {
                MedicineName = "Panadol",
                GenericName = "Paracetamol",
                Form = "Tablet",
                Strength = "500 mg",
                Dose = "1-2 tabs",
                Frequency = "TDS",
                Timing = "Morning, Afternoon, Night",
                MealRelation = MealRelation.AfterMeal,
                Route = "Oral",
                Duration = "3 days",
                Instructions = "Take when temperature > 99.5F",
                SortOrder = 1
            });

            rx.AddMedicine(new PrescriptionMedicine
            {
                MedicineName = "Risek",
                GenericName = "Omeprazole",
                Form = "Capsule",
                Strength = "20 mg",
                Dose = "1 cap",
                Frequency = "OD",
                Timing = "Morning",
                MealRelation = MealRelation.BeforeMeal,
                Route = "Oral",
                Duration = "5 days",
                Instructions = "Before breakfast",
                SortOrder = 2
            });

            samplePatient.LastVisitDate = rx.PrescriptionDate;
            await context.Prescriptions.AddAsync(rx, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo initial prescription.");
        }
    }
}
