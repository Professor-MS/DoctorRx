using System;

namespace DoctorRx.Domain.Entities;

public class AppMeta
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
