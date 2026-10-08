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

public enum FollowUpMode
{
    None = 0,
    InDays = 1,
    InWeeks = 2,
    InMonths = 3,
    CustomDate = 4,
    SOS = 5,
    PRN = 6
}

public class FollowUpSettingState
{
    public FollowUpType Type { get; set; } = FollowUpType.None;
    public int? IntervalValue { get; set; }
    public string? IntervalUnit { get; set; } // "Days", "Weeks", "Months"
    public DateOnly? SpecificDate { get; set; }
    public string? CustomText { get; set; }

    // Phase 2 Unified Mode properties
    public FollowUpMode Mode { get; set; } = FollowUpMode.None;
    public int Interval { get; set; } = 7;
    public DateOnly CustomDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    public DateOnly? CalculateDate(DateOnly visitDate)
    {
        return Mode switch
        {
            FollowUpMode.InDays when Interval > 0 => visitDate.AddDays(Interval),
            FollowUpMode.InWeeks when Interval > 0 => visitDate.AddDays(Interval * 7),
            FollowUpMode.InMonths when Interval > 0 => visitDate.AddMonths(Interval),
            FollowUpMode.CustomDate => CustomDate,
            _ => GetEffectiveDate(visitDate)
        };
    }

    /// <summary>
    /// Computes the effective target follow-up date based on current visit date.
    /// </summary>
    public DateOnly? GetEffectiveDate(DateOnly visitDate)
    {
        if (Mode != FollowUpMode.None)
        {
            return CalculateDate(visitDate);
        }

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
        if (Mode != FollowUpMode.None)
        {
            var target = CalculateDate(visitDate);
            return Mode switch
            {
                FollowUpMode.InDays when Interval > 0 => $"After {Interval} days ({target:ddd, dd MMM yyyy})",
                FollowUpMode.InWeeks when Interval > 0 => $"After {Interval} weeks ({target:ddd, dd MMM yyyy})",
                FollowUpMode.InMonths when Interval > 0 => $"After {Interval} months ({target:ddd, dd MMM yyyy})",
                FollowUpMode.CustomDate => $"Follow-up on {CustomDate:ddd, dd MMM yyyy}",
                FollowUpMode.SOS => "Review SOS (if symptoms worsen)",
                FollowUpMode.PRN => "Review PRN (as needed)",
                _ => string.Empty
            };
        }

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
