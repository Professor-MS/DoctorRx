using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Services;

public class BackupRetentionPolicy
{
    /// <summary>
    /// Pure GFS retention algorithm.
    /// Evaluates which backups to keep and which to delete based on daily, weekly, and monthly rules.
    /// Guarantees that if a backup is the only verified backup available, it is never selected for deletion.
    /// </summary>
    public static RetentionPrunePlan EvaluateBackupsToPrune(
        IReadOnlyList<BackupMetadataDto> allBackups,
        int dailyCount = 7,
        int weeklyCount = 4,
        int monthlyCount = 3)
    {
        if (allBackups == null || allBackups.Count == 0)
        {
            return new RetentionPrunePlan(Array.Empty<BackupMetadataDto>(), Array.Empty<BackupMetadataDto>());
        }

        var sortedBackups = allBackups.OrderByDescending(b => b.CreatedAtUtc).ToList();
        var keptSet = new HashSet<BackupMetadataDto>();

        // 1. Daily tier: keep newest backup for each distinct date (up to dailyCount dates)
        var dailyGroups = sortedBackups
            .GroupBy(b => b.CreatedAtUtc.Date)
            .OrderByDescending(g => g.Key)
            .Take(dailyCount);

        foreach (var group in dailyGroups)
        {
            // Pick newest in that day
            keptSet.Add(group.OrderByDescending(b => b.CreatedAtUtc).First());
        }

        // 2. Weekly tier: keep newest backup for each distinct ISO week (up to weeklyCount weeks)
        var weeklyGroups = sortedBackups
            .GroupBy(b => GetIsoYearAndWeek(b.CreatedAtUtc))
            .OrderByDescending(g => g.Key)
            .Take(weeklyCount);

        foreach (var group in weeklyGroups)
        {
            keptSet.Add(group.OrderByDescending(b => b.CreatedAtUtc).First());
        }

        // 3. Monthly tier: keep newest backup for each distinct month (up to monthlyCount months)
        var monthlyGroups = sortedBackups
            .GroupBy(b => (b.CreatedAtUtc.Year, b.CreatedAtUtc.Month))
            .OrderByDescending(g => g.Key)
            .Take(monthlyCount);

        foreach (var group in monthlyGroups)
        {
            keptSet.Add(group.OrderByDescending(b => b.CreatedAtUtc).First());
        }

        // Potential candidates for deletion
        var candidatesToDelete = sortedBackups.Where(b => !keptSet.Contains(b)).ToList();

        // Safety Guard: Never delete a backup that is the ONLY verified one.
        // Check if keptSet contains at least one verified backup
        bool anyKeptIsVerified = keptSet.Any(b => b.IsVerified);
        var finalToDelete = new List<BackupMetadataDto>();

        foreach (var candidate in candidatesToDelete)
        {
            if (candidate.IsVerified && !anyKeptIsVerified)
            {
                // Preserve this verified backup from deletion
                keptSet.Add(candidate);
                anyKeptIsVerified = true;
            }
            else
            {
                finalToDelete.Add(candidate);
            }
        }

        return new RetentionPrunePlan(keptSet.OrderByDescending(b => b.CreatedAtUtc).ToList(), finalToDelete);
    }

    /// <summary>
    /// Pure retention evaluation for safety backups (pre-migration and pre-restore).
    /// Keeps the newest maxSafetyBackups (default 5).
    /// </summary>
    public static RetentionPrunePlan EvaluateSafetyBackupsToPrune(
        IReadOnlyList<BackupMetadataDto> safetyBackups,
        int maxSafetyBackups = 5)
    {
        if (safetyBackups == null || safetyBackups.Count == 0)
        {
            return new RetentionPrunePlan(Array.Empty<BackupMetadataDto>(), Array.Empty<BackupMetadataDto>());
        }

        var sorted = safetyBackups.OrderByDescending(b => b.CreatedAtUtc).ToList();
        var toKeep = sorted.Take(maxSafetyBackups).ToList();
        var toDelete = sorted.Skip(maxSafetyBackups).ToList();

        return new RetentionPrunePlan(toKeep, toDelete);
    }

    private static (int Year, int Week) GetIsoYearAndWeek(DateTime utc)
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        int week = calendar.GetWeekOfYear(utc, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return (utc.Year, week);
    }
}

public record RetentionPrunePlan(
    IReadOnlyList<BackupMetadataDto> KeptBackups,
    IReadOnlyList<BackupMetadataDto> BackupsToDelete
);
