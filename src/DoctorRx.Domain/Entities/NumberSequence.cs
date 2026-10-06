using System;

namespace DoctorRx.Domain.Entities;

public class NumberSequence
{
    public string SequenceKey { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
