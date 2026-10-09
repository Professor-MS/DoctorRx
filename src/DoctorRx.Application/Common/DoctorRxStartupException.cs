using System;

namespace DoctorRx.Application.Common;

public enum StartupFailureReason
{
    NetworkDriveDetected,
    InsufficientDiskSpace,
    AccessDenied,
    DatabaseLocked,
    DatabaseCorrupt,
    NewerVersionDatabase,
    FailedMigration,
    LegacyDatabaseMigrationRequired
}

public class DoctorRxStartupException : Exception
{
    public StartupFailureReason Reason { get; }
    public string UserGuidance { get; }

    public DoctorRxStartupException(StartupFailureReason reason, string message, string userGuidance, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        UserGuidance = userGuidance;
    }
}
