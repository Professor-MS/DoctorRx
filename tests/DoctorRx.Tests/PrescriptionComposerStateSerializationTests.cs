using System;
using System.Collections.Generic;
using System.Text.Json;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;
using Xunit;

namespace DoctorRx.Tests;

public class PrescriptionComposerStateSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    [Fact]
    public void Serialization_RoundTrip_PreservesAllFieldsDateOnlyAndUrduUnicode()
    {
        var originalState = new PrescriptionComposerState
        {
            DraftKey = Guid.NewGuid(),
            SchemaVersion = 1,
            PatientId = 42,
            PatientName = "محمد علی خان",
            PatientRecordNumber = "P-000042",
            PatientAgeText = "35 yrs",
            PatientGender = Gender.Male,
            PatientPhone = "+92 300 1234567",
            PatientKnownAllergies = "پینسلین سے الرجی",
            PatientLastVisitDate = new DateOnly(2026, 9, 15),
            VisitDate = new DateOnly(2026, 10, 8),
            ChiefComplaints = "شدید بخار اور کھانسی",
            BloodPressure = "120/80",
            PulseRate = "72",
            Temperature = "98.6 F",
            WeightKg = "70",
            ClinicalNotes = "Chest clear on auscultation",
            GeneralAdvice = "مکمل آرام کریں اور گرم پانی پئیں",
            FollowUp = new FollowUpSettingState
            {
                Type = FollowUpType.AfterInterval,
                IntervalValue = 2,
                IntervalUnit = "Weeks",
                SpecificDate = null,
                CustomText = null
            },
            Items = new List<PrescriptionMedicineRowState>
            {
                new()
                {
                    RowId = Guid.NewGuid(),
                    MedicineId = 101,
                    MedicineName = "اگمنٹن",
                    GenericName = "Co-Amoxiclav",
                    Form = "ٹیبلٹ",
                    Strength = "625 mg",
                    Dose = "1 گولی",
                    Frequency = "صبح اور شام",
                    Timing = "کھانے کے بعد",
                    MealRelation = MealRelation.AfterMeal,
                    CustomMealRelationText = null,
                    WithWhat = "پانی",
                    Duration = "5 دن",
                    Route = "Oral",
                    Instructions = "کورس مکمل کریں",
                    AddToCatalogIfCustom = false
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalState, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrescriptionComposerState>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalState.DraftKey, deserialized.DraftKey);
        Assert.Equal(originalState.PatientId, deserialized.PatientId);
        Assert.Equal("محمد علی خان", deserialized.PatientName);
        Assert.Equal(new DateOnly(2026, 10, 8), deserialized.VisitDate);
        Assert.Equal(new DateOnly(2026, 9, 15), deserialized.PatientLastVisitDate);
        Assert.Equal("شدید بخار اور کھانسی", deserialized.ChiefComplaints);
        Assert.Equal("مکمل آرام کریں اور گرم پانی پئیں", deserialized.GeneralAdvice);

        Assert.Equal(FollowUpType.AfterInterval, deserialized.FollowUp.Type);
        Assert.Equal(2, deserialized.FollowUp.IntervalValue);
        Assert.Equal("Weeks", deserialized.FollowUp.IntervalUnit);
        Assert.Equal(new DateOnly(2026, 10, 22), deserialized.FollowUp.GetEffectiveDate(deserialized.VisitDate));

        Assert.Single(deserialized.Items);
        var item = deserialized.Items[0];
        Assert.Equal(originalState.Items[0].RowId, item.RowId);
        Assert.Equal(101, item.MedicineId);
        Assert.Equal("اگمنٹن", item.MedicineName);
        Assert.Equal("ٹیبلٹ", item.Form);
        Assert.Equal("625 mg", item.Strength);
        Assert.Equal("1 گولی", item.Dose);
        Assert.Equal("صبح اور شام", item.Frequency);
        Assert.Equal(MealRelation.AfterMeal, item.MealRelation);
        Assert.False(item.AddToCatalogIfCustom);
    }

    [Fact]
    public void Deserialization_ToleratesMissingAndExtraFields()
    {
        // JSON containing an extra field from a future app version and missing some current fields
        var json = @"
        {
            ""draftKey"": ""3f524a80-1a2b-4c3d-8e4f-5a6b7c8d9e0f"",
            ""patientId"": 15,
            ""visitDate"": ""2026-10-08"",
            ""items"": [
                {
                    ""rowId"": ""4a5b6c7d-8e9f-0a1b-2c3d-4e5f6a7b8c9d"",
                    ""medicineName"": ""Brufen"",
                    ""dose"": ""1 tab"",
                    ""frequency"": ""TDS"",
                    ""futureSuperField"": ""ignored safely""
                }
            ],
            ""newFutureFeaturePayload"": { ""some"": ""data"" }
        }";

        var deserialized = JsonSerializer.Deserialize<PrescriptionComposerState>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(Guid.Parse("3f524a80-1a2b-4c3d-8e4f-5a6b7c8d9e0f"), deserialized.DraftKey);
        Assert.Equal(15, deserialized.PatientId);
        Assert.Equal(new DateOnly(2026, 10, 8), deserialized.VisitDate);
        Assert.Single(deserialized.Items);
        Assert.Equal("Brufen", deserialized.Items[0].MedicineName);
        Assert.Equal("1 tab", deserialized.Items[0].Dose);
        Assert.Equal(string.Empty, deserialized.Items[0].Form); // Defaults gracefully to empty
    }
}
