using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Exceptions;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class PrescriptionMedicineSnapshotTests
{
    [Fact]
    public void PrescribedMedicine_PreservesOriginalValues_WhenMasterMedicineChanges()
    {
        // Arrange: Master medicine catalog entry
        var masterMedicine = new Medicine
        {
            Id = 101,
            Name = "Panadol",
            GenericName = "Paracetamol",
            Form = "Tablet",
            Strength = "500 mg"
        };

        // Act: Prescribing this medicine snapshots its identification
        var prescriptionItem = PrescriptionMedicine.FromMedicine(masterMedicine, sortOrder: 1);
        prescriptionItem.Dose = "2 tablets";
        prescriptionItem.Frequency = "TDS";
        prescriptionItem.Route = "Oral";
        prescriptionItem.Duration = "5 days";

        // Now simulate the master catalog being modified later (e.g. renamed or new formulation)
        masterMedicine.Name = "Panadol Extra";
        masterMedicine.Strength = "650 mg";
        masterMedicine.Form = "Caplet";

        // Assert: The historical prescription medicine snapshot MUST remain unchanged
        Assert.Equal("Panadol", prescriptionItem.MedicineName);
        Assert.Equal("500 mg", prescriptionItem.Strength);
        Assert.Equal("Tablet", prescriptionItem.Form);
        Assert.Equal("2 tablets", prescriptionItem.Dose);
        Assert.Equal("5 days", prescriptionItem.Duration);
    }

    [Fact]
    public void Prescription_LifecycleTransitions_EnforcesValidPathways()
    {
        var doctor = new Doctor { Id = 1, Name = "Dr. Tariq", ClinicName = "Al-Shifa" };
        var patient = new Patient { Id = 1, Name = "Abdul Rehman", RecordNumber = "P-000001", DateOfBirth = new DateOnly(1980, 1, 1) };

        var rx = Prescription.CreateFinalized(
            prescriptionNumber: "RX-20261004-0001",
            patientId: patient.Id,
            doctorId: doctor.Id,
            prescriptionDate: new DateOnly(2026, 10, 4),
            doctorSnapshot: doctor.ToSnapshot(),
            patientSnapshot: patient.ToSnapshot(new DateOnly(2026, 10, 4)),
            finalizedAtUtc: DateTime.UtcNow
        );

        Assert.Equal(PrescriptionStatus.Finalized, rx.Status);

        // Transition to Cancelled
        var cancelTime = DateTime.UtcNow;
        rx.Cancel("Dosage error reported by physician", cancelTime);
        Assert.Equal(PrescriptionStatus.Cancelled, rx.Status);
        Assert.Equal("Dosage error reported by physician", rx.CancellationReason);
        Assert.Equal(cancelTime, rx.CancelledAtUtc);

        // Attempting to cancel again must fail
        Assert.Throws<DomainRuleException>(() => rx.Cancel("Second cancellation attempt", DateTime.UtcNow));

        // Attempting to supersede a cancelled prescription must fail
        Assert.Throws<DomainRuleException>(() => rx.MarkSuperseded(DateTime.UtcNow));
    }

    [Fact]
    public async Task Prescription_DtoLevelSnapshotTest_ReturnsSnapshotsAfterLiveEntitiesChange()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(options);
        var uowFactory = new UnitOfWorkFactory(factory);
        var clock = new SystemClock();
        var logger = NullLogger<PrescriptionService>.Instance;

        // Seed initial doctor and patient
        await using (var context = factory.CreateDbContext())
        {
            var doc = new Doctor
            {
                Id = 1,
                Name = "Dr. Tariq Mahmood",
                Qualification = "MBBS, FCPS",
                RegistrationNumber = "12345-P",
                Specialization = "Cardiologist",
                ClinicName = "Heart Care Clinic",
                ClinicAddress = "Main Boulevard, Lahore",
                IsActive = true
            };
            var pat = new Patient
            {
                Id = 1,
                RecordNumber = "P-000042",
                Name = "Original Patient Name",
                DateOfBirth = new DateOnly(1990, 5, 20),
                Age = 36,
                Gender = Gender.Female,
                Phone = "+92 300 1112233",
                Address = "Old Address, Lahore"
            };
            var med = new Medicine
            {
                Id = 1,
                Name = "Original Med",
                Form = "Tablet",
                Strength = "10 mg"
            };

            context.Doctors.Add(doc);
            context.Patients.Add(pat);
            context.Medicines.Add(med);
            await context.SaveChangesAsync();
        }

        var rxService = new PrescriptionService(uowFactory, clock, logger);

        var createDto = new CreatePrescriptionDto
        {
            PatientId = 1,
            DoctorId = 1,
            PrescriptionDate = new DateOnly(2026, 10, 5),
            ChiefComplaints = "Chest tightness",
            Items = new List<CreatePrescriptionMedicineDto>
            {
                new()
                {
                    MedicineId = 1,
                    MedicineName = "Original Med",
                    Form = "Tablet",
                    Strength = "10 mg",
                    Dose = "1 tab",
                    Frequency = "OD",
                    Route = "Oral",
                    Duration = "7 days"
                }
            }
        };

        var createResult = await rxService.FinalizePrescriptionAsync(createDto);
        Assert.True(createResult.IsSuccess, createResult.ErrorMessage);
        int prescriptionId = createResult.Value!.Id;

        // Act: Mutate the live Doctor, Patient, and Medicine catalog in database
        await using (var context = factory.CreateDbContext())
        {
            var liveDoc = await context.Doctors.FindAsync(1);
            liveDoc!.Name = "Dr. Completely Altered";
            liveDoc.ClinicName = "Altered Clinic";

            var livePat = await context.Patients.FindAsync(1);
            livePat!.Name = "Renamed Patient";
            livePat.Address = "Moved to Karachi";

            var liveMed = await context.Medicines.FindAsync(1);
            liveMed!.Name = "Altered Med Brand";

            await context.SaveChangesAsync();
        }

        // Retrieve prescription DTO via service
        var fetchedRx = await rxService.GetPrescriptionByIdAsync(prescriptionId);

        // Assert: DTO contains the frozen snapshot values, completely insulated from live mutations
        Assert.NotNull(fetchedRx);
        Assert.Equal("Dr. Tariq Mahmood", fetchedRx.DoctorSnapshot.Name);
        Assert.Equal("Heart Care Clinic", fetchedRx.DoctorSnapshot.ClinicName);
        Assert.Equal("Original Patient Name", fetchedRx.PatientSnapshot.Name);
        Assert.Equal("+92 300 1112233", fetchedRx.PatientSnapshot.Phone);
        Assert.Equal("36 yrs", fetchedRx.PatientSnapshot.AgeText);
        Assert.Single(fetchedRx.Items);
        Assert.Equal("Original Med", fetchedRx.Items[0].MedicineName);
        Assert.Equal("10 mg", fetchedRx.Items[0].Strength);
        Assert.Equal("Tablet", fetchedRx.Items[0].Form);
    }
}
