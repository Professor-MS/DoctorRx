using System;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.Common;

/// <summary>
/// Central plain-language display formatter for clinical and domain enums.
/// Ensures human-friendly names are used instead of code identifiers.
/// </summary>
public static class EnumDisplayFormatter
{
    public static string GetDisplayName(FollowUpMode mode) => mode switch
    {
        FollowUpMode.None => "None",
        FollowUpMode.InDays => "Days",
        FollowUpMode.InWeeks => "Weeks",
        FollowUpMode.InMonths => "Months",
        FollowUpMode.CustomDate => "Custom Date",
        FollowUpMode.SOS => "As needed (SOS)",
        FollowUpMode.PRN => "When required (PRN)",
        _ => mode.ToString()
    };

    public static string GetDisplayName(PrescriptionStatus status) => status switch
    {
        PrescriptionStatus.Finalized => "Issued (locked)",
        PrescriptionStatus.Cancelled => "Cancelled",
        PrescriptionStatus.Superseded => "Superseded",
        _ => status.ToString()
    };

    public static string GetDisplayName(Gender gender) => gender switch
    {
        Gender.NotSpecified => "Not Specified",
        Gender.Male => "Male",
        Gender.Female => "Female",
        Gender.Other => "Other",
        _ => gender.ToString()
    };

    public static string GetDisplayName(MealRelation mealRelation) => mealRelation switch
    {
        MealRelation.AsDirected => "As Directed",
        MealRelation.BeforeMeal => "Before Meal",
        MealRelation.AfterMeal => "After Meal",
        MealRelation.WithMeal => "With Meal",
        MealRelation.EmptyStomach => "On Empty Stomach",
        MealRelation.Bedtime => "At Bedtime",
        MealRelation.Other => "Other",
        _ => mealRelation.ToString()
    };

    public static string GetDisplayName(RouteOfAdministration route) => route switch
    {
        RouteOfAdministration.Oral => "Oral",
        RouteOfAdministration.Topical => "Topical",
        RouteOfAdministration.Inhalation => "Inhalation",
        RouteOfAdministration.Intravenous => "Intravenous (IV)",
        RouteOfAdministration.Intramuscular => "Intramuscular (IM)",
        RouteOfAdministration.Subcutaneous => "Subcutaneous (SC)",
        RouteOfAdministration.Ophthalmic => "Ophthalmic (Eye)",
        RouteOfAdministration.Otic => "Otic (Ear)",
        RouteOfAdministration.Nasal => "Nasal",
        RouteOfAdministration.Sublingual => "Sublingual",
        RouteOfAdministration.Rectal => "Rectal",
        RouteOfAdministration.Other => "Other",
        _ => route.ToString()
    };
}
