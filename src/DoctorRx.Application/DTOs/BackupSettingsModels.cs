using System;

namespace DoctorRx.Application.DTOs;

public class BackupSettings
{
    public bool AutoBackupEnabled { get; set; } = true;
    public int AutoBackupIntervalHours { get; set; } = 4;
    public string? CustomPrimaryDirectory { get; set; }
    public string? SecondaryDirectory { get; set; }
    public int DailyRetention { get; set; } = 7;
    public int WeeklyRetention { get; set; } = 4;
    public int MonthlyRetention { get; set; } = 3;
    public int SafetyRetention { get; set; } = 5;
}

public class BackupStatus
{
    public DateTime? LastSuccessfulBackupUtc { get; set; }
    public string? LastBackupFilePath { get; set; }
    public bool LastBackupVerified { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public bool LastAttemptSuccess { get; set; } = true;
    public string? LastAttemptErrorMessage { get; set; }

    public bool IsStale(DateTime utcNow)
    {
        if (!LastSuccessfulBackupUtc.HasValue) return true;
        return (utcNow - LastSuccessfulBackupUtc.Value).TotalDays > 7;
    }

    public bool HasFailure => !LastAttemptSuccess;
}
