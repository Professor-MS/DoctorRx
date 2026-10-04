using System;

namespace DoctorRx.Domain.Common;

/// <summary>
/// Base class for entities that track creation and update timestamps in UTC.
/// </summary>
public abstract class AuditableEntity : EntityBase
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
