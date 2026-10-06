using System;

namespace DoctorRx.Domain.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
