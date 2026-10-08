using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.Services;

/// <summary>
/// Presentation-agnostic formatter converting prescription medicine rows into plain, human-readable instructions.
/// Adheres strictly to the clinical safety principle:
/// - Plain language only (no shorthand like OD, BD, TDS, SOS, 1-0-1).
/// - Skip empty fields without leaving stray separators or punctuation.
/// - Preserves doctor's text and Unicode/Urdu text exactly as typed.
/// </summary>
public static class MedicineInstructionFormatter
{
    /// <summary>
    /// Formats the formulation header line: e.g. "Amoxicillin 500 mg Capsule"
    /// </summary>
    public static string FormatHeader(PrescriptionMedicineRowState row)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(row.MedicineName))
        {
            parts.Add(row.MedicineName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(row.Strength))
        {
            parts.Add(row.Strength.Trim());
        }

        if (!string.IsNullOrWhiteSpace(row.Form))
        {
            parts.Add(row.Form.Trim());
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Formats the dosage, frequency, and duration line: e.g. "1 capsule × Three times daily × 7 days"
    /// </summary>
    public static string FormatDosageSchedule(PrescriptionMedicineRowState row)
    {
        var scheduleParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(row.Dose))
        {
            scheduleParts.Add(row.Dose.Trim());
        }

        if (!string.IsNullOrWhiteSpace(row.Frequency))
        {
            scheduleParts.Add(row.Frequency.Trim());
        }

        if (!string.IsNullOrWhiteSpace(row.Duration))
        {
            scheduleParts.Add(row.Duration.Trim());
        }

        return string.Join(" × ", scheduleParts);
    }

    /// <summary>
    /// Formats the administration directives: e.g. "After meal, with water, Morning, Oral"
    /// </summary>
    public static string FormatAdministration(PrescriptionMedicineRowState row)
    {
        var adminParts = new List<string>();

        // Meal relation
        var mealText = GetMealRelationText(row.MealRelation, row.CustomMealRelationText);
        if (!string.IsNullOrWhiteSpace(mealText))
        {
            adminParts.Add(mealText);
        }

        // With-what
        if (!string.IsNullOrWhiteSpace(row.WithWhat))
        {
            var withWhat = row.WithWhat.Trim();
            if (withWhat.StartsWith("with ", StringComparison.OrdinalIgnoreCase) ||
                withWhat.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            {
                adminParts.Add(withWhat);
            }
            else
            {
                adminParts.Add($"with {withWhat}");
            }
        }

        // Timing
        if (!string.IsNullOrWhiteSpace(row.Timing))
        {
            adminParts.Add(row.Timing.Trim());
        }

        // Route (only if non-empty, and if not obvious or as specified)
        if (!string.IsNullOrWhiteSpace(row.Route))
        {
            adminParts.Add(row.Route.Trim());
        }

        // Special instructions
        if (!string.IsNullOrWhiteSpace(row.Instructions))
        {
            adminParts.Add(row.Instructions.Trim());
        }

        return string.Join(", ", adminParts);
    }

    /// <summary>
    /// Returns 1 to 3 plain readable lines for the medicine row.
    /// </summary>
    public static IReadOnlyList<string> FormatLines(PrescriptionMedicineRowState row)
    {
        var lines = new List<string>();

        var header = FormatHeader(row);
        if (!string.IsNullOrWhiteSpace(header))
        {
            lines.Add(header);
        }

        var dosage = FormatDosageSchedule(row);
        if (!string.IsNullOrWhiteSpace(dosage))
        {
            lines.Add(dosage);
        }

        var admin = FormatAdministration(row);
        if (!string.IsNullOrWhiteSpace(admin))
        {
            lines.Add(admin);
        }

        return lines;
    }

    /// <summary>
    /// Returns a single unified summary string separated by " / " or custom separator.
    /// </summary>
    public static string FormatSummary(PrescriptionMedicineRowState row, string separator = " / ")
    {
        var lines = FormatLines(row);
        return string.Join(separator, lines);
    }

    private static string GetMealRelationText(MealRelation relation, string? customText)
    {
        return relation switch
        {
            MealRelation.BeforeMeal => "Before meal",
            MealRelation.AfterMeal => "After meal",
            MealRelation.WithMeal => "With meal",
            MealRelation.EmptyStomach => "On empty stomach",
            MealRelation.Bedtime => "At bedtime",
            MealRelation.Other => customText?.Trim() ?? string.Empty,
            _ => customText?.Trim() ?? string.Empty
        };
    }
}
