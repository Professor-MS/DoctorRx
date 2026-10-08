using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class PatientService : IPatientService
{
    private static readonly Regex PhoneAllowedCharsRegex = new(@"^[0-9+\s\-()]+$", RegexOptions.Compiled);

    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<PatientService> _logger;

    public PatientService(IUnitOfWorkFactory uowFactory, IClock clock, ILogger<PatientService> logger)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PagedResult<PatientDto>> GetPatientsPagedAsync(int pageNumber, int pageSize = 50, bool showArchived = false, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 50;

        await using var uow = _uowFactory.Create();
        var totalCount = await uow.Patients.GetTotalCountAsync(showArchived, cancellationToken);
        var patients = await uow.Patients.GetPagedAsync(pageNumber, pageSize, showArchived, cancellationToken);
        var dtos = patients.Select(p => MapToDto(p, _clock.Today)).ToList();

        return new PagedResult<PatientDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<PatientDto>> GetFilteredPatientsPagedAsync(PatientFilterCriteria criteria, CancellationToken cancellationToken = default)
    {
        var pageNumber = criteria.PageNumber < 1 ? 1 : criteria.PageNumber;
        var pageSize = criteria.PageSize < 1 ? 50 : criteria.PageSize;

        await using var uow = _uowFactory.Create();
        var (patients, totalCount) = await uow.Patients.SearchFilteredPagedAsync(
            criteria.SearchQuery,
            (int)criteria.StatusFilter,
            (int)criteria.SortOption,
            pageNumber,
            pageSize,
            cancellationToken);

        var dtos = patients.Select(p => MapToDto(p, _clock.Today)).ToList();
        return new PagedResult<PatientDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string query, int maxResults = 50, bool showArchived = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await using var uow = _uowFactory.Create();
            var paged = await uow.Patients.GetPagedAsync(1, maxResults, showArchived, cancellationToken);
            return paged.Select(p => MapToDto(p, _clock.Today)).ToList();
        }

        await using var uowSearch = _uowFactory.Create();
        var patients = await uowSearch.Patients.SearchAsync(query, maxResults, showArchived, cancellationToken);
        return patients.Select(p => MapToDto(p, _clock.Today)).ToList();
    }

    public async Task<IReadOnlyList<PatientDto>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var patients = await uow.Patients.GetRecentPatientsAsync(count, cancellationToken);
        return patients.Select(p => MapToDto(p, _clock.Today)).ToList();
    }

    public async Task<PatientDto?> GetPatientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var patient = await uow.Patients.GetByIdAsync(id, cancellationToken);
        return patient is null ? null : MapToDto(patient, _clock.Today);
    }

    public async Task<Result<PatientDto>> CreatePatientAsync(CreatePatientDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default)
    {
        var validationResult = ValidatePatientData(dto.Name, dto.DateOfBirth, dto.Age, dto.Phone, dto.Address, dto.MedicalHistoryNotes, dto.KnownAllergies);
        if (!validationResult.IsSuccess)
        {
            return Result<PatientDto>.Failure(validationResult.ErrorMessage!);
        }

        var normalizedName = SearchNormalizer.Normalize(dto.Name);
        var phoneDigits = SearchNormalizer.NormalizePhoneDigits(dto.Phone);

        try
        {
            await using var uow = _uowFactory.Create();

            if (!allowDuplicate && !string.IsNullOrEmpty(phoneDigits))
            {
                var duplicate = await uow.Patients.FindDuplicateAsync(normalizedName, phoneDigits, cancellationToken);
                if (duplicate != null)
                {
                    return Result<PatientDto>.Failure($"DUPLICATE_WARNING: A patient with this name and phone already exists (Record #{duplicate.RecordNumber}).");
                }
            }

            var totalCount = await uow.Patients.GetTotalCountAsync(showArchived: true, cancellationToken);
            var recordNumber = $"P-{(totalCount + 1):D6}";

            var patient = new Patient
            {
                RecordNumber = recordNumber,
                Name = dto.Name.Trim(),
                DateOfBirth = dto.DateOfBirth,
                Age = dto.Age,
                AgeRecordedDate = dto.Age.HasValue ? _clock.Today : null,
                Gender = dto.Gender,
                Phone = dto.Phone?.Trim(),
                Address = dto.Address?.Trim(),
                MedicalHistoryNotes = dto.MedicalHistoryNotes?.Trim(),
                KnownAllergies = dto.KnownAllergies?.Trim(),
                CreatedAtUtc = _clock.UtcNow
            };
            patient.RefreshSearchFields();

            await uow.Patients.AddAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient created successfully with Id {PatientId}", patient.Id);
            return Result<PatientDto>.Success(MapToDto(patient, _clock.Today));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create patient");
            return Result<PatientDto>.Failure("Unable to save patient due to a storage error. Please try again.");
        }
    }

    public async Task<Result<PatientDto>> UpdatePatientAsync(UpdatePatientDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0)
        {
            return Result<PatientDto>.Failure("Invalid patient ID.");
        }

        var validationResult = ValidatePatientData(dto.Name, dto.DateOfBirth, dto.Age, dto.Phone, dto.Address, dto.MedicalHistoryNotes, dto.KnownAllergies);
        if (!validationResult.IsSuccess)
        {
            return Result<PatientDto>.Failure(validationResult.ErrorMessage!);
        }

        try
        {
            await using var uow = _uowFactory.Create();
            var patient = await uow.Patients.GetByIdAsync(dto.Id, cancellationToken);
            if (patient == null)
            {
                return Result<PatientDto>.Failure($"Patient with ID #{dto.Id} was not found.");
            }

            patient.Name = dto.Name.Trim();
            patient.DateOfBirth = dto.DateOfBirth;
            patient.Age = dto.Age;
            if (dto.Age.HasValue && patient.Age != dto.Age)
            {
                patient.AgeRecordedDate = _clock.Today;
            }
            patient.Gender = dto.Gender;
            patient.Phone = dto.Phone?.Trim();
            patient.Address = dto.Address?.Trim();
            patient.MedicalHistoryNotes = dto.MedicalHistoryNotes?.Trim();
            patient.KnownAllergies = dto.KnownAllergies?.Trim();
            patient.UpdatedAtUtc = _clock.UtcNow;
            patient.RefreshSearchFields();

            await uow.Patients.UpdateAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient updated with Id {PatientId}", patient.Id);
            return Result<PatientDto>.Success(MapToDto(patient, _clock.Today));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update patient Id {PatientId}", dto.Id);
            return Result<PatientDto>.Failure("Unable to update patient. Please check the information and try again.");
        }
    }

    public async Task<Result> ArchivePatientAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var patient = await uow.Patients.GetByIdAsync(id, cancellationToken);
            if (patient == null) return Result.Failure($"Patient #{id} not found.");

            patient.Archive(_clock.UtcNow);
            await uow.Patients.UpdateAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient archived with Id {PatientId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive patient Id {PatientId}", id);
            return Result.Failure("Unable to archive patient.");
        }
    }

    public async Task<Result> RestorePatientAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var patient = await uow.Patients.GetByIdAsync(id, cancellationToken);
            if (patient == null) return Result.Failure($"Patient #{id} not found.");

            patient.Restore(_clock.UtcNow);
            await uow.Patients.UpdateAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient restored with Id {PatientId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore patient Id {PatientId}", id);
            return Result.Failure("Unable to restore patient.");
        }
    }

    public async Task<Result> DeletePatientAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var patient = await uow.Patients.GetWithPrescriptionsAsync(id, cancellationToken);
            if (patient == null)
            {
                return Result.Failure($"Patient with ID #{id} was not found.");
            }

            if (patient.Prescriptions.Any())
            {
                return Result.Failure("Cannot delete a patient who has associated prescription records. Prescriptions must be preserved for medical auditing.");
            }

            await uow.Patients.DeleteAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient deleted with Id {PatientId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete patient Id {PatientId}", id);
            return Result.Failure("Unable to delete patient due to a storage error.");
        }
    }

    private Result ValidatePatientData(string? name, DateOnly? dob, int? age, string? phone, string? address, string? notes, string? allergies)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Patient name is required.");
        }
        if (name.Length > 150)
        {
            return Result.Failure("Patient name cannot exceed 150 characters.");
        }

        if (dob.HasValue && dob.Value > _clock.Today)
        {
            return Result.Failure("Date of birth cannot be in the future.");
        }

        if (age is < 0 or > 130)
        {
            return Result.Failure("Age must be between 0 and 130.");
        }

        if (dob.HasValue && age.HasValue)
        {
            var calculatedAge = _clock.Today.Year - dob.Value.Year;
            if (dob.Value > _clock.Today.AddYears(-calculatedAge)) calculatedAge--;
            if (Math.Abs(calculatedAge - age.Value) > 1)
            {
                return Result.Failure("Entered age is inconsistent with the provided date of birth.");
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (phone.Length > 30)
            {
                return Result.Failure("Phone number cannot exceed 30 characters.");
            }
            if (!PhoneAllowedCharsRegex.IsMatch(phone))
            {
                return Result.Failure("Phone number contains invalid characters.");
            }
            var digitsOnly = SearchNormalizer.NormalizePhoneDigits(phone);
            if (digitsOnly.Length is < 7 or > 20)
            {
                return Result.Failure("Phone number must contain between 7 and 20 digits.");
            }
        }

        if (address != null && address.Length > 300) return Result.Failure("Address cannot exceed 300 characters.");
        if (notes != null && notes.Length > 1000) return Result.Failure("Medical history notes cannot exceed 1000 characters.");
        if (allergies != null && allergies.Length > 500) return Result.Failure("Known allergies cannot exceed 500 characters.");

        return Result.Success();
    }

    private static PatientDto MapToDto(Patient p, DateOnly today)
    {
        return new PatientDto(
            p.Id,
            p.RecordNumber,
            p.Name,
            p.DateOfBirth,
            p.CalculateAge(today),
            p.Gender,
            p.Phone,
            p.Address,
            p.MedicalHistoryNotes,
            p.KnownAllergies,
            p.CreatedAtUtc,
            p.LastVisitDate,
            p.IsArchived,
            p.ArchivedAtUtc
        );
    }
}
