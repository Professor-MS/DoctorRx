using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class DraftService : IDraftService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<DraftService> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _draftLocks = new();
    private readonly ConcurrentDictionary<Guid, byte> _finalizedKeys = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DraftService(
        IUnitOfWorkFactory uowFactory,
        IClock clock,
        ILogger<DraftService> logger)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _logger = logger;
    }

    public void MarkFinalized(Guid draftKey)
    {
        _finalizedKeys.TryAdd(draftKey, 0);
    }

    public bool IsFinalized(Guid draftKey)
    {
        return _finalizedKeys.ContainsKey(draftKey);
    }

    public async Task<Result<Guid>> SaveAsync(PrescriptionComposerState state, CancellationToken cancellationToken = default)
    {
        if (state == null)
        {
            return Result<Guid>.Failure("Composer state cannot be null.");
        }

        // Zombie draft prevention (Amendment 1): refuse write if state is finalized or marked finalized
        if (state.IsFinalized || _finalizedKeys.ContainsKey(state.DraftKey))
        {
            _logger.LogInformation("Refusing to save finalized draft {DraftKey}", state.DraftKey);
            return Result<Guid>.Failure("Cannot save a finalized prescription as draft.");
        }

        // Serialize saves per draft (Amendment 8: serialized, last write wins)
        var gate = _draftLocks.GetOrAdd(state.DraftKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check finalized status after acquiring serialization lock
            if (state.IsFinalized || _finalizedKeys.ContainsKey(state.DraftKey))
            {
                return Result<Guid>.Failure("Cannot save a finalized prescription as draft.");
            }

            state.UpdatedAtUtc = _clock.UtcNow;
            var jsonPayload = JsonSerializer.Serialize(state, JsonOptions);

            await using var uow = _uowFactory.Create();
            await using var tx = await uow.BeginWriteTransactionAsync(cancellationToken);

            var existing = await uow.Drafts.GetByDraftKeyAsync(state.DraftKey, cancellationToken);
            if (existing != null)
            {
                existing.PatientId = state.PatientId;
                existing.PayloadJson = jsonPayload;
                existing.PayloadVersion = state.SchemaVersion;
                existing.UpdatedAtUtc = state.UpdatedAtUtc;
                await uow.Drafts.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                var newDraft = new Draft
                {
                    DraftKey = state.DraftKey,
                    PatientId = state.PatientId,
                    PayloadJson = jsonPayload,
                    PayloadVersion = state.SchemaVersion,
                    CreatedAtUtc = state.CreatedAtUtc,
                    UpdatedAtUtc = state.UpdatedAtUtc,
                    AppVersion = "1.0.0"
                };
                await uow.Drafts.AddAsync(newDraft, cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Draft {DraftKey} saved successfully with {MedicineCount} items", state.DraftKey, state.Items.Count);
            return Result<Guid>.Success(state.DraftKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save draft {DraftKey}", state.DraftKey);
            return Result<Guid>.Failure("Unable to save draft due to a storage error.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<PrescriptionComposerState>> GetAsync(Guid draftKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var draft = await uow.Drafts.GetByDraftKeyAsync(draftKey, cancellationToken);
            if (draft == null)
            {
                return Result<PrescriptionComposerState>.Failure("Draft not found.");
            }

            PrescriptionComposerState? state;
            try
            {
                state = JsonSerializer.Deserialize<PrescriptionComposerState>(draft.PayloadJson, JsonOptions);
            }
            catch (Exception pex)
            {
                _logger.LogError(pex, "Corrupt draft payload for draft {DraftKey}", draftKey);
                return Result<PrescriptionComposerState>.Failure("Draft payload is corrupted.");
            }

            if (state == null)
            {
                return Result<PrescriptionComposerState>.Failure("Draft payload is empty.");
            }

            // Refresh patient display data from database, never trust payload copies (Amendment 3)
            if (state.PatientId.HasValue)
            {
                var freshPatient = await uow.Patients.GetByIdAsync(state.PatientId.Value, cancellationToken);
                if (freshPatient != null)
                {
                    state.PatientName = freshPatient.Name;
                    state.PatientRecordNumber = freshPatient.RecordNumber;
                    state.PatientAgeText = freshPatient.FormatAgeAsOf(_clock.Today);
                    state.PatientGender = freshPatient.Gender;
                    state.PatientPhone = freshPatient.Phone;
                    state.PatientKnownAllergies = freshPatient.KnownAllergies;
                    state.PatientLastVisitDate = freshPatient.LastVisitDate;
                }
            }

            return Result<PrescriptionComposerState>.Success(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load draft {DraftKey}", draftKey);
            return Result<PrescriptionComposerState>.Failure("Unable to load draft.");
        }
    }

    public async Task<IReadOnlyList<DraftSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var drafts = await uow.Drafts.GetAllAsync(cancellationToken);
            var now = _clock.UtcNow;

            var summaries = new List<DraftSummaryDto>(drafts.Count);
            foreach (var d in drafts)
            {
                var isOlderThan30Days = (now - d.UpdatedAtUtc).TotalDays > 30;

                try
                {
                    var parsed = JsonSerializer.Deserialize<PrescriptionComposerState>(d.PayloadJson, JsonOptions);
                    if (parsed != null)
                    {
                        var patientName = d.Patient?.Name ?? parsed.PatientName ?? "Unknown Patient";
                        var patientRecord = d.Patient?.RecordNumber ?? parsed.PatientRecordNumber;
                        var patientAge = d.Patient?.FormatAgeAsOf(_clock.Today) ?? parsed.PatientAgeText;
                        var patientGender = d.Patient?.Gender ?? parsed.PatientGender;

                        summaries.Add(new DraftSummaryDto(
                            d.DraftKey,
                            d.PatientId,
                            patientName,
                            patientRecord,
                            patientAge,
                            patientGender,
                            parsed.Items?.Count ?? 0,
                            d.CreatedAtUtc,
                            d.UpdatedAtUtc,
                            isOlderThan30Days,
                            IsCorrupt: false,
                            ErrorMessage: null
                        ));
                        continue;
                    }
                }
                catch (Exception pex)
                {
                    _logger.LogWarning(pex, "Corrupt draft payload encountered in draft {DraftKey}", d.DraftKey);
                }

                // If parsing failed or was corrupt, provide safe summary without crashing
                summaries.Add(new DraftSummaryDto(
                    d.DraftKey,
                    d.PatientId,
                    d.Patient?.Name ?? "Unreadable draft",
                    d.Patient?.RecordNumber,
                    null,
                    null,
                    0,
                    d.CreatedAtUtc,
                    d.UpdatedAtUtc,
                    isOlderThan30Days,
                    IsCorrupt: true,
                    ErrorMessage: "Draft data is corrupted"
                ));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list drafts");
            return Array.Empty<DraftSummaryDto>();
        }
    }

    public async Task<Result> DiscardAsync(Guid draftKey, CancellationToken cancellationToken = default)
    {
        var gate = _draftLocks.GetOrAdd(draftKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var uow = _uowFactory.Create();
            await using var tx = await uow.BeginWriteTransactionAsync(cancellationToken);

            var draft = await uow.Drafts.GetByDraftKeyAsync(draftKey, cancellationToken);
            if (draft != null)
            {
                await uow.Drafts.DeleteAsync(draft, cancellationToken);
                await uow.CommitAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Draft {DraftKey} discarded successfully", draftKey);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discard draft {DraftKey}", draftKey);
            return Result.Failure("Unable to discard draft.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            return await uow.Drafts.GetCountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get drafts count");
            return 0;
        }
    }
}
