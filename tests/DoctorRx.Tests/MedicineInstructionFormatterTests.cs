using System;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Enums;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineInstructionFormatterTests
{
    [Fact]
    public void Format_FullRow_ProducesAccurateLinesWithoutStrayPunctuation()
    {
        var row = new PrescriptionMedicineRowState
        {
            MedicineName = "Amoxicillin",
            Strength = "500 mg",
            Form = "Capsule",
            Dose = "1 capsule",
            Frequency = "Three times daily",
            Duration = "7 days",
            MealRelation = MealRelation.AfterMeal,
            WithWhat = "water",
            Timing = "Morning, Afternoon, Night",
            Route = "Oral",
            Instructions = "Complete full course"
        };

        var lines = MedicineInstructionFormatter.FormatLines(row);

        Assert.Equal(3, lines.Count);
        Assert.Equal("Amoxicillin 500 mg Capsule", lines[0]);
        Assert.Equal("1 capsule × Three times daily × 7 days", lines[1]);
        Assert.Equal("After meal, with water, Morning, Afternoon, Night, Oral, Complete full course", lines[2]);

        var summary = MedicineInstructionFormatter.FormatSummary(row);
        Assert.Equal("Amoxicillin 500 mg Capsule / 1 capsule × Three times daily × 7 days / After meal, with water, Morning, Afternoon, Night, Oral, Complete full course", summary);
    }

    [Fact]
    public void Format_MinimalRow_OmitsEmptySegmentsWithoutStraySeparators()
    {
        var row = new PrescriptionMedicineRowState
        {
            MedicineName = "Paracetamol",
            Dose = "1 tablet",
            Frequency = "Twice daily"
            // Form, Strength, Duration, MealRelation, etc. are empty/default
        };

        var lines = MedicineInstructionFormatter.FormatLines(row);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Paracetamol", lines[0]); // No trailing spaces
        Assert.Equal("1 tablet × Twice daily", lines[1]); // No trailing '×'

        // Summary must not have trailing separators
        var summary = MedicineInstructionFormatter.FormatSummary(row);
        Assert.Equal("Paracetamol / 1 tablet × Twice daily", summary);
        Assert.DoesNotContain("× ×", summary);
        Assert.DoesNotContain("/ /", summary);
    }

    [Fact]
    public void Format_CustomValues_PreservesDoctorInputExactly()
    {
        var row = new PrescriptionMedicineRowState
        {
            MedicineName = "Salbutamol",
            Form = "Inhaler",
            Strength = "100 mcg",
            Dose = "2 puffs",
            Frequency = "As needed",
            Duration = "1 month",
            MealRelation = MealRelation.Other,
            CustomMealRelationText = "During shortness of breath",
            WithWhat = "spacer device",
            Route = "Inhalation",
            Instructions = "Rinse mouth after inhalation"
        };

        var lines = MedicineInstructionFormatter.FormatLines(row);

        Assert.Equal(3, lines.Count);
        Assert.Equal("Salbutamol 100 mcg Inhaler", lines[0]);
        Assert.Equal("2 puffs × As needed × 1 month", lines[1]);
        Assert.Equal("During shortness of breath, with spacer device, Inhalation, Rinse mouth after inhalation", lines[2]);
    }

    [Fact]
    public void Format_UrduAndEnglishMixedText_PreservesUnicodeUntouched()
    {
        var row = new PrescriptionMedicineRowState
        {
            MedicineName = "پیناڈول",
            Strength = "500 mg",
            Form = "گولی",
            Dose = "1 گولی",
            Frequency = "دن میں تین بار",
            Duration = "5 دن",
            MealRelation = MealRelation.AfterMeal,
            WithWhat = "پانی کے ساتھ",
            Instructions = "اگر بخار زیادہ ہو تو پٹی کریں"
        };

        var lines = MedicineInstructionFormatter.FormatLines(row);

        Assert.Equal(3, lines.Count);
        Assert.Equal("پیناڈول 500 mg گولی", lines[0]);
        Assert.Equal("1 گولی × دن میں تین بار × 5 دن", lines[1]);
        Assert.Contains("پانی کے ساتھ", lines[2]);
        Assert.Contains("اگر بخار زیادہ ہو تو پٹی کریں", lines[2]);
    }

    [Fact]
    public void Format_DoseOnlyWithoutFrequencyOrDuration_HasNoCrossSeparator()
    {
        var row = new PrescriptionMedicineRowState
        {
            MedicineName = "Oral Rehydration Salts",
            Dose = "1 sachet in 1 liter water"
        };

        var lines = MedicineInstructionFormatter.FormatLines(row);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Oral Rehydration Salts", lines[0]);
        Assert.Equal("1 sachet in 1 liter water", lines[1]);
        Assert.DoesNotContain("×", lines[1]);
    }
}
