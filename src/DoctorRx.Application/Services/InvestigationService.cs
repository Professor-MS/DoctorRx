using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class InvestigationService : IInvestigationService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IDoctorService? _doctorService;
    private readonly ILogger<InvestigationService> _logger;

    public InvestigationService(
        IUnitOfWorkFactory uowFactory,
        IDoctorService? doctorService,
        ILogger<InvestigationService> logger)
    {
        _uowFactory = uowFactory;
        _doctorService = doctorService;
        _logger = logger;
    }

    #region Catalogue Management

    public async Task EnsureDefaultCatalogueSeededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var existingItems = await uow.Investigations.GetCatalogItemsAsync(includeInactive: true, cancellationToken: cancellationToken);
            if (existingItems.Count == 0)
            {
                _logger.LogInformation("Diagnostic investigations catalogue is empty. Seeding initial catalogue ({Count} items)...",
                    DefaultInvestigationCatalogue.InitialEntries.Count);

                foreach (var entry in DefaultInvestigationCatalogue.InitialEntries)
                {
                    var item = new InvestigationCatalogItem
                    {
                        Name = entry.Name,
                        NormalizedName = entry.Name.Trim().ToUpperInvariant(),
                        Category = entry.Category,
                        Subcategory = entry.Subcategory,
                        CommonAbbreviation = entry.Abbreviation,
                        SampleTypeOrModality = entry.SampleOrModality,
                        DefaultInstructions = entry.DefaultInstructions,
                        IsCustom = false,
                        IsActive = true,
                        UsageCount = 0
                    };
                    await uow.Investigations.AddCatalogItemAsync(item, cancellationToken);
                }

                await uow.CommitAsync(cancellationToken);
                _logger.LogInformation("Successfully seeded diagnostic investigations catalogue.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure diagnostic investigations catalogue is seeded.");
        }
    }

    public async Task<IReadOnlyList<InvestigationCatalogItemDto>> GetCatalogItemsAsync(
        string? category = null, 
        bool includeInactive = false, 
        CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var items = await uow.Investigations.GetCatalogItemsAsync(category, includeInactive, cancellationToken);
        return items.Select(MapToCatalogDto).ToList();
    }

    public async Task<IReadOnlyList<InvestigationCatalogItemDto>> SearchCatalogAsync(
        string query, 
        string? category = null, 
        int maxResults = 50, 
        CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var items = await uow.Investigations.SearchCatalogAsync(query, category, maxResults, cancellationToken);
        return items.Select(MapToCatalogDto).ToList();
    }

    public async Task<Result<InvestigationCatalogItemDto>> AddCustomInvestigationAsync(
        CreateInvestigationCatalogItemDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<InvestigationCatalogItemDto>.Failure("Investigation name cannot be empty.", ResultErrorCode.Validation);
        }

        var trimmedName = dto.Name.Trim();
        await using var uow = _uowFactory.Create();

        var existing = await uow.Investigations.FindCatalogItemByNameAsync(trimmedName, cancellationToken);
        if (existing != null)
        {
            return Result<InvestigationCatalogItemDto>.Failure(
                $"An investigation with the name '{trimmedName}' already exists in the catalog.", 
                ResultErrorCode.DuplicateWarning);
        }

        var item = new InvestigationCatalogItem
        {
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "Other Diagnostics" : dto.Category.Trim(),
            Subcategory = dto.Subcategory?.Trim(),
            CommonAbbreviation = dto.CommonAbbreviation?.Trim(),
            SampleTypeOrModality = dto.SampleTypeOrModality?.Trim(),
            DefaultInstructions = dto.DefaultInstructions?.Trim(),
            IsCustom = true,
            IsActive = true,
            UsageCount = 0
        };

        await uow.Investigations.AddCatalogItemAsync(item, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        _logger.LogInformation("Added custom investigation to catalog: {Name} ({Category})", item.Name, item.Category);
        return Result<InvestigationCatalogItemDto>.Success(MapToCatalogDto(item));
    }

    #endregion

    #region Request Orders

    public async Task<Result<InvestigationRequestDetailDto>> CreateInvestigationRequestAsync(
        CreateInvestigationRequestDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (dto.PatientId <= 0)
        {
            return Result<InvestigationRequestDetailDto>.Failure("A valid registered patient must be selected.", ResultErrorCode.Validation);
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return Result<InvestigationRequestDetailDto>.Failure("Please add at least one diagnostic test or examination to the request.", ResultErrorCode.Validation);
        }

        await using var uow = _uowFactory.Create();
        var patient = await uow.Patients.GetByIdAsync(dto.PatientId, cancellationToken);
        if (patient == null)
        {
            return Result<InvestigationRequestDetailDto>.Failure($"Patient with ID {dto.PatientId} was not found.", ResultErrorCode.NotFound);
        }

        DoctorDto? activeDoctor = null;
        if (_doctorService != null)
        {
            activeDoctor = await _doctorService.GetActiveDoctorAsync(cancellationToken);
        }
        if (activeDoctor == null)
        {
            var activeDocEntity = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
            if (activeDocEntity != null)
            {
                activeDoctor = new DoctorDto(
                    activeDocEntity.Id,
                    activeDocEntity.Name,
                    activeDocEntity.Qualification,
                    activeDocEntity.RegistrationNumber,
                    activeDocEntity.Specialization,
                    activeDocEntity.Phone,
                    activeDocEntity.Email,
                    activeDocEntity.ClinicName,
                    activeDocEntity.ClinicAddress,
                    activeDocEntity.ClinicPhone,
                    activeDocEntity.HeaderText,
                    activeDocEntity.FooterText,
                    activeDocEntity.TitlePrefix,
                    activeDocEntity.RegistrationLabel
                );
            }
        }

        var requestNumber = await uow.Investigations.GenerateNextRequestNumberAsync(dto.RequestDate, cancellationToken);

        var request = new InvestigationRequest
        {
            RequestNumber = requestNumber,
            PatientId = patient.Id,
            RequestDate = dto.RequestDate,
            ClinicalNotes = dto.ClinicalNotes?.Trim(),
            Instructions = dto.Instructions?.Trim(),
            Status = "Active",
            DoctorSnapshotJson = activeDoctor != null ? JsonSerializer.Serialize(activeDoctor) : null,
            PatientSnapshotJson = JsonSerializer.Serialize(new
            {
                patient.Name,
                patient.RecordNumber,
                Age = patient.FormatAgeAsOf(dto.RequestDate),
                Gender = patient.Gender.ToString(),
                patient.Phone,
                patient.Address
            })
        };

        int order = 1;
        foreach (var itemDto in dto.Items)
        {
            if (string.IsNullOrWhiteSpace(itemDto.InvestigationName)) continue;

            var requestItem = new InvestigationRequestItem
            {
                CatalogItemId = itemDto.CatalogItemId,
                InvestigationName = itemDto.InvestigationName.Trim(),
                Category = string.IsNullOrWhiteSpace(itemDto.Category) ? "General" : itemDto.Category.Trim(),
                Subcategory = itemDto.Subcategory?.Trim(),
                BodyPartOrSite = itemDto.BodyPartOrSite?.Trim(),
                ViewOrProtocol = itemDto.ViewOrProtocol?.Trim(),
                SpecificInstructions = itemDto.SpecificInstructions?.Trim(),
                DisplayOrder = order++
            };

            request.Items.Add(requestItem);

            if (itemDto.CatalogItemId.HasValue)
            {
                await uow.Investigations.IncrementCatalogUsageAsync(itemDto.CatalogItemId.Value, cancellationToken);
            }
        }

        await uow.Investigations.AddRequestAsync(request, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        _logger.LogInformation("Saved investigation request {RequestNumber} with {Count} tests for patient {PatientId}",
            request.RequestNumber, request.Items.Count, patient.Id);

        // Fetch full entity with populated patient navigation
        var detailed = await uow.Investigations.GetRequestByIdWithDetailsAsync(request.Id, cancellationToken);
        return Result<InvestigationRequestDetailDto>.Success(MapToDetailDto(detailed ?? request, activeDoctor));
    }

    public async Task<InvestigationRequestDetailDto?> GetRequestByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var request = await uow.Investigations.GetRequestByIdWithDetailsAsync(id, cancellationToken);
        if (request == null) return null;

        DoctorDto? activeDoctor = null;
        if (_doctorService != null)
        {
            activeDoctor = await _doctorService.GetActiveDoctorAsync(cancellationToken);
        }

        return MapToDetailDto(request, activeDoctor);
    }

    public async Task<InvestigationRequestDetailDto?> GetRequestByNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var request = await uow.Investigations.GetRequestByNumberWithDetailsAsync(requestNumber, cancellationToken);
        if (request == null) return null;

        DoctorDto? activeDoctor = null;
        if (_doctorService != null)
        {
            activeDoctor = await _doctorService.GetActiveDoctorAsync(cancellationToken);
        }

        return MapToDetailDto(request, activeDoctor);
    }

    public async Task<IReadOnlyList<InvestigationRequestSummaryDto>> GetRequestsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var requests = await uow.Investigations.GetRequestsByPatientIdAsync(patientId, cancellationToken);
        return requests.Select(MapToSummaryDto).ToList();
    }

    public async Task<IReadOnlyList<InvestigationRequestSummaryDto>> GetRecentRequestsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var requests = await uow.Investigations.GetRecentRequestsAsync(count, cancellationToken);
        return requests.Select(MapToSummaryDto).ToList();
    }

    public async Task<IReadOnlyList<InvestigationRequestSummaryDto>> SearchRequestsAsync(
        string? query, 
        DateOnly? fromDate = null, 
        DateOnly? toDate = null, 
        int? patientId = null, 
        int maxResults = 100, 
        CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var requests = await uow.Investigations.SearchRequestsAsync(query, fromDate, toDate, patientId, maxResults, cancellationToken);
        return requests.Select(MapToSummaryDto).ToList();
    }

    #endregion

    #region Mapping Helpers

    private static InvestigationCatalogItemDto MapToCatalogDto(InvestigationCatalogItem item)
    {
        return new InvestigationCatalogItemDto(
            item.Id,
            item.Name,
            item.Category,
            item.Subcategory,
            item.CommonAbbreviation,
            item.SampleTypeOrModality,
            item.DefaultInstructions,
            item.IsCustom,
            item.IsActive,
            item.UsageCount
        );
    }

    private static InvestigationRequestSummaryDto MapToSummaryDto(InvestigationRequest r)
    {
        var patientName = r.Patient?.Name ?? "Unknown Patient";
        var patientRecord = r.Patient?.RecordNumber ?? $"P-{r.PatientId:D4}";
        var patientAge = r.Patient?.FormatAgeAsOf(r.RequestDate) ?? string.Empty;
        var patientGender = r.Patient?.Gender ?? Gender.NotSpecified;

        var itemsSummary = string.Join(", ", r.Items.Take(4).Select(i => i.InvestigationName));
        if (r.Items.Count > 4)
        {
            itemsSummary += $" (+{r.Items.Count - 4} more)";
        }

        return new InvestigationRequestSummaryDto(
            r.Id,
            r.RequestNumber,
            r.PatientId,
            patientName,
            patientRecord,
            patientAge,
            patientGender,
            r.RequestDate,
            r.CreatedAtUtc,
            r.Status,
            r.Items.Count,
            itemsSummary
        );
    }

    private static InvestigationRequestDetailDto MapToDetailDto(InvestigationRequest r, DoctorDto? fallbackDoctor)
    {
        string? doctorName = fallbackDoctor?.DisplayName;
        string? qualification = fallbackDoctor?.Qualification;
        string? registration = fallbackDoctor?.RegistrationNumber;
        string? specialization = fallbackDoctor?.Specialization;
        string? clinicName = fallbackDoctor?.ClinicName;
        string? clinicAddress = fallbackDoctor?.ClinicAddress;
        string? clinicPhone = fallbackDoctor?.ClinicPhone;

        if (!string.IsNullOrWhiteSpace(r.DoctorSnapshotJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.DoctorSnapshotJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("DisplayName", out var nameProp)) doctorName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(doctorName) && root.TryGetProperty("Name", out var rawNameProp))
                {
                    var prefix = root.TryGetProperty("TitlePrefix", out var pProp) ? pProp.GetString() : "Dr.";
                    doctorName = string.IsNullOrWhiteSpace(prefix) ? rawNameProp.GetString() : $"{prefix} {rawNameProp.GetString()}";
                }
                if (root.TryGetProperty("Qualification", out var qualProp)) qualification = qualProp.GetString();
                if (root.TryGetProperty("RegistrationNumber", out var regProp)) registration = regProp.GetString();
                if (root.TryGetProperty("Specialization", out var specProp)) specialization = specProp.GetString();
                if (root.TryGetProperty("ClinicName", out var clinicProp)) clinicName = clinicProp.GetString();
                if (root.TryGetProperty("ClinicAddress", out var addrProp)) clinicAddress = addrProp.GetString();
                if (root.TryGetProperty("ClinicPhone", out var phoneProp)) clinicPhone = phoneProp.GetString();
            }
            catch { }
        }

        var patientName = r.Patient?.Name ?? "Unknown Patient";
        var patientRecord = r.Patient?.RecordNumber ?? $"P-{r.PatientId:D4}";
        var patientAge = r.Patient?.FormatAgeAsOf(r.RequestDate) ?? string.Empty;
        var patientGender = r.Patient?.Gender ?? Gender.NotSpecified;
        var patientPhone = r.Patient?.Phone;
        var patientAddress = r.Patient?.Address;
        var patientAllergies = r.Patient?.KnownAllergies;

        var items = r.Items
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new InvestigationRequestItemDto(
                i.Id,
                i.RequestId,
                i.CatalogItemId,
                i.InvestigationName,
                i.Category,
                i.Subcategory,
                i.BodyPartOrSite,
                i.ViewOrProtocol,
                i.SpecificInstructions,
                i.DisplayOrder
            ))
            .ToList();

        return new InvestigationRequestDetailDto(
            r.Id,
            r.RequestNumber,
            r.PatientId,
            patientName,
            patientRecord,
            patientAge,
            patientGender,
            patientPhone,
            patientAddress,
            patientAllergies,
            r.RequestDate,
            r.CreatedAtUtc,
            r.Status,
            r.ClinicalNotes,
            r.Instructions,
            doctorName,
            qualification,
            registration,
            specialization,
            clinicName,
            clinicAddress,
            clinicPhone,
            items
        );
    }

    #endregion
}
