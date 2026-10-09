using System;
using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Application.Interfaces;

public record DatabaseHealthReport(
    bool IsHealthy,
    bool QuickCheckPassed,
    bool IntegrityCheckPassed,
    string? ErrorDetails,
    string? CorruptQuarantinePath,
    bool SearchIndexHealthy,
    long DatabaseSizeBytes
);

public interface IDatabaseHealthService
{
    Task<DatabaseHealthReport> RunQuickCheckAsync(CancellationToken cancellationToken = default);
    Task<DatabaseHealthReport> RunFullIntegrityCheckAsync(CancellationToken cancellationToken = default);
    Task<string?> QuarantineCorruptDatabaseAsync(CancellationToken cancellationToken = default);
    Task CheckpointWalAsync(CancellationToken cancellationToken = default);
    bool IsNetworkOrUncPath(string path);
    long GetAvailableFreeSpaceBytes();
}
