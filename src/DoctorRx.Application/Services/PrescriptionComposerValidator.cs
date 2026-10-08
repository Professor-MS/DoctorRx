using System;
using System.Collections.Generic;
using System.Linq;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;

namespace DoctorRx.Application.Services;

public class PrescriptionComposerValidator : IPrescriptionComposerValidator
{
    private readonly IClock _clock;

    public PrescriptionComposerValidator(IClock clock)
    {
        _clock = clock;
    }

    private sealed class FallbackClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    }

    public static ComposerValidationResult ValidateState(PrescriptionComposerState state, IClock? clock = null)
    {
        return new PrescriptionComposerValidator(clock ?? new FallbackClock()).Validate(state);
    }

    public ComposerValidationResult Validate(PrescriptionComposerState state)
    {
        var result = new ComposerValidationResult();

        // 1. Prescription-level Errors
        if (state.PatientId == null || state.PatientId <= 0)
        {
            result.Errors.Add(new ValidationIssue("PatientId", null, "A patient must be selected."));
        }

        if (state.Items == null || state.Items.Count == 0)
        {
            result.Errors.Add(new ValidationIssue("Items", null, "Prescription must contain at least one medicine."));
        }

        // Field length limits on prescription-level fields
        CheckLength(result.Errors, "ChiefComplaints", null, state.ChiefComplaints, 1000, "Chief complaints");
        CheckLength(result.Errors, "BloodPressure", null, state.BloodPressure, 20, "Blood pressure");
        CheckLength(result.Errors, "PulseRate", null, state.PulseRate, 20, "Pulse rate");
        CheckLength(result.Errors, "Temperature", null, state.Temperature, 20, "Temperature");
        CheckLength(result.Errors, "WeightKg", null, state.WeightKg, 20, "Weight");
        CheckLength(result.Errors, "ClinicalNotes", null, state.ClinicalNotes, 2000, "Clinical notes");
        CheckLength(result.Errors, "GeneralAdvice", null, state.GeneralAdvice, 2000, "Doctor's advice");
        CheckLength(result.Errors, "FollowUp.CustomText", null, state.FollowUp?.CustomText, 200, "Follow-up text");

        // 2. Prescription-level Warnings
        if (state.VisitDate > _clock.Today)
        {
            result.Warnings.Add(new ValidationIssue("VisitDate", null, "Visit date is in the future."));
        }

        // 3. Medicine row evaluation
        if (state.Items != null && state.Items.Count > 0)
        {
            // Track duplicates: normalized (name + strength)
            var seenKeys = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < state.Items.Count; i++)
            {
                var row = state.Items[i];
                int rowNumber = i + 1;

                // Required Errors per Amendment 4: missing name, dose, frequency, or length violations
                if (string.IsNullOrWhiteSpace(row.MedicineName))
                {
                    result.Errors.Add(new ValidationIssue("MedicineName", row.RowId, $"Medicine #{rowNumber}: Medicine name cannot be blank."));
                }
                if (string.IsNullOrWhiteSpace(row.Dose))
                {
                    result.Errors.Add(new ValidationIssue("Dose", row.RowId, $"Medicine #{rowNumber} ('{row.MedicineName}'): Dose is required."));
                }
                if (string.IsNullOrWhiteSpace(row.Frequency))
                {
                    result.Errors.Add(new ValidationIssue("Frequency", row.RowId, $"Medicine #{rowNumber} ('{row.MedicineName}'): Frequency is required."));
                }

                // Row field length limits
                CheckLength(result.Errors, "MedicineName", row.RowId, row.MedicineName, 150, $"Medicine #{rowNumber} name");
                CheckLength(result.Errors, "GenericName", row.RowId, row.GenericName, 150, $"Medicine #{rowNumber} generic name");
                CheckLength(result.Errors, "Form", row.RowId, row.Form, 50, $"Medicine #{rowNumber} form");
                CheckLength(result.Errors, "Strength", row.RowId, row.Strength, 50, $"Medicine #{rowNumber} strength");
                CheckLength(result.Errors, "Dose", row.RowId, row.Dose, 50, $"Medicine #{rowNumber} dose");
                CheckLength(result.Errors, "Frequency", row.RowId, row.Frequency, 50, $"Medicine #{rowNumber} frequency");
                CheckLength(result.Errors, "Timing", row.RowId, row.Timing, 100, $"Medicine #{rowNumber} timing");
                CheckLength(result.Errors, "CustomMealRelationText", row.RowId, row.CustomMealRelationText, 100, $"Medicine #{rowNumber} meal relation text");
                CheckLength(result.Errors, "WithWhat", row.RowId, row.WithWhat, 100, $"Medicine #{rowNumber} with-what");
                CheckLength(result.Errors, "Route", row.RowId, row.Route, 50, $"Medicine #{rowNumber} route");
                CheckLength(result.Errors, "Duration", row.RowId, row.Duration, 50, $"Medicine #{rowNumber} duration");
                CheckLength(result.Errors, "Instructions", row.RowId, row.Instructions, 500, $"Medicine #{rowNumber} instructions");

                // Warnings (non-blocking)
                // Amendment 4: Missing Form is a WARNING, not an error
                if (string.IsNullOrWhiteSpace(row.Form))
                {
                    result.Warnings.Add(new ValidationIssue("Form", row.RowId, $"Medicine #{rowNumber} ('{row.MedicineName}'): Dosage form is not specified."));
                }

                // Missing Duration
                if (string.IsNullOrWhiteSpace(row.Duration))
                {
                    result.Warnings.Add(new ValidationIssue("Duration", row.RowId, $"Medicine #{rowNumber} ('{row.MedicineName}'): Duration is not specified."));
                }

                // Very long duration text
                if (!string.IsNullOrWhiteSpace(row.Duration) && row.Duration.Trim().Length > 30)
                {
                    result.Warnings.Add(new ValidationIssue("Duration", row.RowId, $"Medicine #{rowNumber} ('{row.MedicineName}'): Duration text is unusually long."));
                }

                // Duplicate tracking
                if (!string.IsNullOrWhiteSpace(row.MedicineName))
                {
                    var normName = SearchNormalizer.Normalize(row.MedicineName);
                    var normStrength = SearchNormalizer.Normalize(row.Strength);
                    var comboKey = $"{normName}::{normStrength}";

                    if (!seenKeys.TryGetValue(comboKey, out var rowIds))
                    {
                        rowIds = new List<Guid>();
                        seenKeys[comboKey] = rowIds;
                    }
                    rowIds.Add(row.RowId);
                }
            }

            // Flag duplicates as warnings
            foreach (var kvp in seenKeys)
            {
                if (kvp.Value.Count > 1)
                {
                    foreach (var dupRowId in kvp.Value)
                    {
                        result.Warnings.Add(new ValidationIssue("MedicineName", dupRowId, "This medicine (same name and strength) appears more than once in this prescription."));
                    }
                }
            }
        }

        return result;
    }

    private static void CheckLength(List<ValidationIssue> errors, string fieldKey, Guid? rowId, string? value, int max, string displayName)
    {
        if (value != null && value.Length > max)
        {
            errors.Add(new ValidationIssue(fieldKey, rowId, $"{displayName} exceeds maximum length of {max} characters (currently {value.Length})."));
        }
    }
}
