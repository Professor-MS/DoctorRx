using System;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
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
            Strength = "500 mg",
            DefaultDose = "1 tablet",
            DefaultFrequency = "TDS",
            DefaultRoute = "Oral",
            DefaultInstructions = "Take after food"
        };

        // Act: Prescribing this medicine snapshots its state
        var prescriptionItem = PrescriptionMedicine.FromMedicine(masterMedicine, sortOrder: 1);
        prescriptionItem.Dose = "2 tablets";
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
    public void FinalizePrescription_LocksStatusToFinalized()
    {
        // Arrange
        var prescription = new Prescription
        {
            PrescriptionNumber = "RX-20261004-0001",
            PatientId = 1,
            DoctorId = 1,
            Status = PrescriptionStatus.Draft
        };

        // Act
        prescription.FinalizePrescription();

        // Assert
        Assert.Equal(PrescriptionStatus.Finalized, prescription.Status);
    }
}
