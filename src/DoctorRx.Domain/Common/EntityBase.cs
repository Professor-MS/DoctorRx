using System;

namespace DoctorRx.Domain.Common;

/// <summary>
/// Base class for all domain entities with an integer primary key.
/// </summary>
public abstract class EntityBase
{
    public int Id { get; set; }
}
