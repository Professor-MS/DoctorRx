using System;
using System.Collections.Generic;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Interfaces;
using Xunit;

namespace DoctorRx.Tests;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

public class PrescriptionComposerValidatorTests
{
    private readonly FakeClock _clock = new();
    private readonly PrescriptionComposerValidator _validator;

    public PrescriptionComposerValidatorTests()
    {
        _validator = new PrescriptionComposerValidator(_clock);
    }

    private static PrescriptionComposerState CreateValidState()
    {
        return new PrescriptionComposerState
        {
            PatientId = 1,
            VisitDate = new DateOnly(2026, 10, 8),
            Items = new List<PrescriptionMedicineRowState>
            {
                new()
                {
                    MedicineName = "Amoxicillin",
                    Form = "Capsule",
                    Strength = "500 mg",
                    Dose = "1 cap",
                    Frequency = "Three times daily",
                    Duration = "7 days"
                }
            }
        };
    }

    [Fact]
    public void Validate_ValidState_HasZeroErrorsAndZeroWarnings()
    {
        var state = CreateValidState();

        var result = _validator.Validate(state);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_MissingPatient_ReturnsPatientIdError()
    {
        var state = CreateValidState();
        state.PatientId = null;

        var result = _validator.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldKey == "PatientId");
    }

    [Fact]
    public void Validate_NoMedicines_ReturnsItemsError()
    {
        var state = CreateValidState();
        state.Items.Clear();

        var result = _validator.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldKey == "Items");
    }

    [Fact]
    public void Validate_MissingMedicineNameDoseOrFrequency_ReturnsRowErrors()
    {
        var state = CreateValidState();
        var row = state.Items[0];
        row.MedicineName = "";
        row.Dose = "  ";
        row.Frequency = "";

        var result = _validator.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldKey == "MedicineName" && e.RowId == row.RowId);
        Assert.Contains(result.Errors, e => e.FieldKey == "Dose" && e.RowId == row.RowId);
        Assert.Contains(result.Errors, e => e.FieldKey == "Frequency" && e.RowId == row.RowId);
    }

    [Fact]
    public void Validate_FieldLengthExceeded_ReturnsLengthError()
    {
        var state = CreateValidState();
        state.ChiefComplaints = new string('A', 1001); // max 1000
        state.Items[0].MedicineName = new string('B', 151); // max 150

        var result = _validator.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldKey == "ChiefComplaints");
        Assert.Contains(result.Errors, e => e.FieldKey == "MedicineName");
    }

    [Fact]
    public void Validate_MissingForm_IsWarningNotError_PerAmendment4()
    {
        var state = CreateValidState();
        state.Items[0].Form = ""; // Missing form

        var result = _validator.Validate(state);

        // Crucial requirement of Amendment 4: Form is a WARNING, does NOT block finalization
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, w => w.FieldKey == "Form" && w.RowId == state.Items[0].RowId);
    }

    [Fact]
    public void Validate_MissingDuration_IsWarningNotError()
    {
        var state = CreateValidState();
        state.Items[0].Duration = ""; // Missing duration

        var result = _validator.Validate(state);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.FieldKey == "Duration" && w.RowId == state.Items[0].RowId);
    }

    [Fact]
    public void Validate_DuplicateMedicine_ReturnsWarningOnBothRows()
    {
        var state = CreateValidState();
        var row1 = state.Items[0];
        var row2 = new PrescriptionMedicineRowState
        {
            MedicineName = "amoxicillin ", // same normalized name
            Strength = "500 mg",
            Form = "Capsule",
            Dose = "2 caps",
            Frequency = "Twice daily",
            Duration = "5 days"
        };
        state.Items.Add(row2);

        var result = _validator.Validate(state);

        Assert.True(result.IsValid); // Still valid to finalize if doctor intentionally repeats
        Assert.Contains(result.Warnings, w => w.FieldKey == "MedicineName" && w.RowId == row1.RowId);
        Assert.Contains(result.Warnings, w => w.FieldKey == "MedicineName" && w.RowId == row2.RowId);
    }

    [Fact]
    public void Validate_FutureVisitDate_ReturnsWarning()
    {
        var state = CreateValidState();
        state.VisitDate = _clock.Today.AddDays(2); // In the future

        var result = _validator.Validate(state);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.FieldKey == "VisitDate");
    }

    [Fact]
    public void Validate_VeryLongDurationText_ReturnsWarning()
    {
        var state = CreateValidState();
        state.Items[0].Duration = "For six consecutive weeks total"; // 32 chars (> 30 and <= 50)

        var result = _validator.Validate(state);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.FieldKey == "Duration" && w.RowId == state.Items[0].RowId);
    }
}
