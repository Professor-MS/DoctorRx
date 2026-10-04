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

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly DoctorRxDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(DoctorRxDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Ensuring SQLite database is created and initialized...");
            await _context.Database.EnsureCreatedAsync(cancellationToken);

            await SeedDataAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the SQLite database.");
            throw;
        }
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        // 1. Seed Doctor if none exists
        if (!await _context.Doctors.AnyAsync(cancellationToken))
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

            await _context.Doctors.AddAsync(doctor, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded default active doctor profile.");
        }

        // 2. Seed Medicines catalog if empty
        if (!await _context.Medicines.AnyAsync(cancellationToken))
        {
            var medicines = new List<Medicine>
            {
                new() { Name = "Panadol", GenericName = "Paracetamol", Form = "Tablet", Strength = "500 mg", DefaultDose = "1-2 tablets", DefaultFrequency = "TDS (8 hourly)", DefaultRoute = "Oral", DefaultInstructions = "Take after meals for fever/pain", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Augmentin", GenericName = "Amoxicillin + Clavulanic Acid", Form = "Tablet", Strength = "625 mg", DefaultDose = "1 tablet", DefaultFrequency = "BD (12 hourly)", DefaultRoute = "Oral", DefaultInstructions = "Complete 5 days antibiotic course", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Flagyl", GenericName = "Metronidazole", Form = "Tablet", Strength = "400 mg", DefaultDose = "1 tablet", DefaultFrequency = "TDS (8 hourly)", DefaultRoute = "Oral", DefaultInstructions = "Take after meal, avoid empty stomach", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Risek", GenericName = "Omeprazole", Form = "Capsule", Strength = "20 mg", DefaultDose = "1 capsule", DefaultFrequency = "OD (Once daily)", DefaultRoute = "Oral", DefaultInstructions = "Take 30 minutes before breakfast", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Brufen", GenericName = "Ibuprofen", Form = "Tablet", Strength = "400 mg", DefaultDose = "1 tablet", DefaultFrequency = "BD (After meals)", DefaultRoute = "Oral", DefaultInstructions = "Do not take on empty stomach", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Ponstan", GenericName = "Mefenamic Acid", Form = "Tablet", Strength = "500 mg", DefaultDose = "1 tablet", DefaultFrequency = "SOS (When needed)", DefaultRoute = "Oral", DefaultInstructions = "For acute pain relief", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Glucophage", GenericName = "Metformin HCl", Form = "Tablet", Strength = "500 mg", DefaultDose = "1 tablet", DefaultFrequency = "BD (With meals)", DefaultRoute = "Oral", DefaultInstructions = "Take with or immediately after meals", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Arinac Forte", GenericName = "Ibuprofen + Pseudoephedrine", Form = "Tablet", Strength = "400/60 mg", DefaultDose = "1 tablet", DefaultFrequency = "BD (12 hourly)", DefaultRoute = "Oral", DefaultInstructions = "For nasal congestion and headache", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Hydryllin", GenericName = "Aminophylline Compound", Form = "Syrup", Strength = "120 ml", DefaultDose = "2 teaspoons", DefaultFrequency = "TDS (8 hourly)", DefaultRoute = "Oral", DefaultInstructions = "For productive chest cough", CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Ciproxin", GenericName = "Ciprofloxacin", Form = "Tablet", Strength = "500 mg", DefaultDose = "1 tablet", DefaultFrequency = "BD (12 hourly)", DefaultRoute = "Oral", DefaultInstructions = "Drink plenty of water", CreatedAtUtc = DateTime.UtcNow }
            };

            await _context.Medicines.AddRangeAsync(medicines, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded initial medicine catalog.");
        }

        // 3. Seed Sample Patients if empty
        if (!await _context.Patients.AnyAsync(cancellationToken))
        {
            var patients = new List<Patient>
            {
                new()
                {
                    Name = "Abdul Rehman",
                    DateOfBirth = new DateTime(1982, 5, 14),
                    Age = 44,
                    Gender = Gender.Male,
                    Phone = "+92 333 4567890",
                    Address = "House 12, Street 7, Sector F-10/2, Islamabad",
                    MedicalHistoryNotes = "Hypertension diagnosed 2022. Well controlled.",
                    KnownAllergies = "Sulfa drugs",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-20)
                },
                new()
                {
                    Name = "Fatima Bibi",
                    DateOfBirth = new DateTime(1995, 11, 23),
                    Age = 30,
                    Gender = Gender.Female,
                    Phone = "+92 301 9876543",
                    Address = "Apartment 302, Silver Oaks, F-10, Islamabad",
                    MedicalHistoryNotes = "No major chronic illnesses reported.",
                    KnownAllergies = "Penicillin (rash)",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    Name = "Muhammad Usman",
                    DateOfBirth = new DateTime(2012, 3, 8),
                    Age = 14,
                    Gender = Gender.Male,
                    Phone = "+92 321 5551234",
                    Address = "Sector G-9/1, Islamabad",
                    MedicalHistoryNotes = "Occasional seasonal allergic rhinitis.",
                    KnownAllergies = "None reported",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            };

            await _context.Patients.AddRangeAsync(patients, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded sample patients.");

            // Create a sample prescription for Abdul Rehman
            var doctor = await _context.Doctors.FirstAsync(cancellationToken);
            var samplePatient = patients[0];

            var rx = new Prescription
            {
                PrescriptionNumber = $"RX-{DateTime.Today:yyyyMMdd}-0001",
                PatientId = samplePatient.Id,
                DoctorId = doctor.Id,
                PrescriptionDate = DateTime.Today,
                ChiefComplaints = "Fever and mild throat soreness for 2 days",
                BloodPressure = "120/80",
                PulseRate = "78 bpm",
                Temperature = "100.4 F",
                WeightKg = "76 kg",
                ClinicalNotes = "Pharyngeal erythema observed. Chest clear to auscultation.",
                GeneralAdvice = "Drink warm fluids, salt water gargles 3 times daily. Complete rest.",
                FollowUpDate = DateTime.Today.AddDays(5),
                Status = PrescriptionStatus.Finalized,
                CreatedAtUtc = DateTime.UtcNow
            };

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

            await _context.Prescriptions.AddAsync(rx, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded sample initial prescription.");
        }
    }
}
