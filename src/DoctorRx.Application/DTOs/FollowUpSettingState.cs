using System;

namespace DoctorRx.Application.DTOs;

public enum FollowUpType
{
    None = 0,
    AfterInterval = 1,
    OnSpecificDate = 2,
    ReviewIfSymptomsPersist = 3,
    NoFollowUpRequired = 4,
    Custom = 5
}

public class FollowUpSettingState
{
    public FollowUpType Type { get; set; } = FollowUpType.None;
    public int? IntervalValue { get; set; }
    public string? IntervalUnit { get; set; } // "Days", "Weeks", "Months"
    public DateOnly? SpecificDate { get; set; }
    public string? CustomText { get; set; }

    /// <summary>
    /// Computes the effective target follow-up date based on current visit date.
    /// </summary>
    public DateOnly? GetEffectiveDate(DateOnly visitDate)
    {
        return Type switch
        {
            FollowUpType.AfterInterval when IntervalValue.HasValue && IntervalValue > 0 =>
                (IntervalUnit?.ToLowerInvariant()) switch
                {
                    "weeks" or "week" => visitDate.AddDays(IntervalValue.Value * 7),
                    "months" or "month" => visitDate.AddMonths(IntervalValue.Value),
                    _ => visitDate.AddDays(IntervalValue.Value)
                },
            FollowUpType.OnSpecificDate => SpecificDate,
            _ => null
        };
    }

    /// <summary>
    /// Gets formatted human-readable directive text for prescription summary and print.
    /// </summary>
    public string GetDisplayText(DateOnly visitDate)
    {
        return Type switch
        {
            FollowUpType.None => string.Empty,
            FollowUpType.ReviewIfSymptomsPersist => "Review if symptoms persist",
            FollowUpType.NoFollowUpRequired => "No follow-up required",
            FollowUpType.AfterInterval when IntervalValue.HasValue =>
                $"After {IntervalValue} {IntervalUnit?.ToLowerInvariant() ?? "days"} ({GetEffectiveDate(visitDate):yyyy-MM-dd})",
            FollowUpType.OnSpecificDate when SpecificDate.HasValue =>
                $"Follow-up on {SpecificDate.Value:yyyy-MM-dd}",
            FollowUpType.Custom => CustomText?.Trim() ?? string.Empty,
            _ => string.Empty
        };
    }
}
