using System;
using System.Linq;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Models;
using Xunit;

namespace DoctorRx.Tests;

public class EnumDisplayCompletenessTests
{
    [Fact]
    public void FollowUpMode_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<FollowUpMode>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            // Multi-word / camel-case values must differ from their raw ToString()
            if (val is FollowUpMode.InDays or FollowUpMode.InWeeks or FollowUpMode.InMonths or FollowUpMode.CustomDate or FollowUpMode.SOS or FollowUpMode.PRN)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void PrescriptionStatus_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<PrescriptionStatus>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            if (val == PrescriptionStatus.Finalized)
            {
                Assert.Equal("Issued (locked)", text);
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void Gender_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<Gender>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            if (val == Gender.NotSpecified)
            {
                Assert.Equal("Not Specified", text);
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void MealRelation_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<MealRelation>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            // Multi-word values differ from raw
            if (val is MealRelation.AsDirected or MealRelation.BeforeMeal or MealRelation.AfterMeal or MealRelation.WithMeal or MealRelation.EmptyStomach or MealRelation.Bedtime)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void RouteOfAdministration_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<RouteOfAdministration>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            // Multi-word / abbreviation values differ from raw
            if (val is RouteOfAdministration.Intravenous or RouteOfAdministration.Intramuscular or RouteOfAdministration.Subcutaneous or RouteOfAdministration.Ophthalmic or RouteOfAdministration.Otic)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void PrescriptionDatePreset_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<PrescriptionDatePreset>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            if (val is PrescriptionDatePreset.All or PrescriptionDatePreset.Last7Days or PrescriptionDatePreset.ThisMonth or PrescriptionDatePreset.Custom)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void PrescriptionStatusSearchFilter_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<PrescriptionStatusSearchFilter>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            if (val is PrescriptionStatusSearchFilter.Default or PrescriptionStatusSearchFilter.All)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }

    [Fact]
    public void PrescriptionSortOption_AllValuesHandled_AndMultiWordValuesDifferFromRaw()
    {
        var values = Enum.GetValues<PrescriptionSortOption>();
        foreach (var val in values)
        {
            var text = EnumDisplayFormatter.GetDisplayName(val);
            Assert.False(string.IsNullOrWhiteSpace(text));

            if (val is PrescriptionSortOption.NewestFirst or PrescriptionSortOption.OldestFirst or PrescriptionSortOption.PatientName or PrescriptionSortOption.PrescriptionNumber)
            {
                Assert.NotEqual(val.ToString(), text);
            }
        }
    }
}
