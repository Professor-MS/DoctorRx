using System;
using System.Collections.Generic;

namespace DoctorRx.Application.DTOs;

public record BackupManifest(
    int FormatVersion,
    string AppVersion,
    string LatestMigrationId,
    DateTime CreatedAtUtc,
    Dictionary<string, int> TableRowCounts,
    string DatabaseSha256,
    long DatabaseSizeBytes
);

public record BackupResult(
    bool Success,
    string? BackupFilePath,
    string? ErrorMessage,
    BackupManifest? Manifest,
    bool IsVerified
);

public record BackupVerificationResult(
    bool IsValid,
    string StatusSummary,
    string? ErrorMessage,
    string? CheckedIntegrityPragma,
    string? CheckedForeignKeyPragma,
    bool HashMatched,
    bool RowCountsMatched
);

public record BackupMetadataDto(
    string FilePath,
    string FileName,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    bool IsVerified,
    BackupManifest? Manifest
);
