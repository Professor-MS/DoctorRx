using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class MedicineService : IMedicineService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<MedicineService> _logger;
    private readonly IMedicineSearchService _searchService;

    public MedicineService(
        IUnitOfWorkFactory uowFactory,
        IClock clock,
        ILogger<MedicineService> logger,
        IMedicineSearchService? searchService = null)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _logger = logger;
        _searchService = searchService ?? new MedicineSearchService(uowFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<MedicineSearchService>.Instance);
    }

    public async Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 50;

        await using var uow = _uowFactory.Create();
        var list = await uow.Medicines.FindAsync(m => m.IsActive, cancellationToken);
        return list.OrderBy(m => m.Name)
                   .Skip((pageNumber - 1) * pageSize)
                   .Take(pageSize)
                   .Select(MapToDto)
                   .ToList();
    }

    public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        return _searchService.SearchAsync(query, maxResults, cancellationToken);
    }

    public Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(MedicineSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        return _searchService.SearchAsync(criteria, cancellationToken);
    }

    public async Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var medicine = await uow.Medicines.GetByIdAsync(id, cancellationToken);
        return medicine is null ? null : MapToDto(medicine);
    }

    public async Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<MedicineDto>.Failure("Medicine brand or name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Form))
        {
            return Result<MedicineDto>.Failure("Dosage form is required.");
        }

        if (dto.Name.Trim().Length > 150)
        {
            return Result<MedicineDto>.Failure("Medicine name cannot exceed 150 characters.");
        }

        if (dto.GenericName != null && dto.GenericName.Trim().Length > 150)
        {
            return Result<MedicineDto>.Failure("Generic name cannot exceed 150 characters.");
        }

        if (dto.Form.Trim().Length > 50)
        {
            return Result<MedicineDto>.Failure("Dosage form cannot exceed 50 characters.");
        }

        if (dto.Strength != null && dto.Strength.Trim().Length > 50)
        {
            return Result<MedicineDto>.Failure("Strength cannot exceed 50 characters.");
        }

        try
        {
            await using var uow = _uowFactory.Create();

            if (!allowDuplicate)
            {
                var duplicates = await uow.Medicines.FindPotentialDuplicatesAsync(
                    dto.Name.Trim(),
                    dto.Form.Trim(),
                    dto.Strength?.Trim(),
                    cancellationToken: cancellationToken);

                if (duplicates.Count > 0)
                {
                    var dup = duplicates[0];
                    if (dup.IsActive)
                    {
                        return Result<MedicineDto>.Failure(
                            $"DUPLICATE_WARNING: A medicine with this formulation already exists in the catalog ('{dup.DisplayTitle}').");
                    }
                    else
                    {
                        return Result<MedicineDto>.Failure(
                            $"DUPLICATE_WARNING: An inactive medicine with this formulation already exists in the catalog ('{dup.DisplayTitle}'). You can reactivate it instead.");
                    }
                }
            }

            var medicine = new Medicine
            {
                Name = dto.Name.Trim(),
                GenericName = dto.GenericName?.Trim(),
                Form = dto.Form.Trim(),
                Strength = dto.Strength?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAtUtc = _clock.UtcNow
            };
            medicine.RefreshSearchFields();

            await uow.Medicines.AddAsync(medicine, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Medicine created: {Id} ('{Name}')", medicine.Id, medicine.Name);
            return Result<MedicineDto>.Success(MapToDto(medicine));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create medicine");
            return Result<MedicineDto>.Failure("Failed to add medicine to catalog.");
        }
    }

    public async Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, bool allowDuplicate = false, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0) return Result<MedicineDto>.Failure("Invalid medicine ID.");

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<MedicineDto>.Failure("Medicine name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Form))
        {
            return Result<MedicineDto>.Failure("Dosage form is required.");
        }

        if (dto.Name.Trim().Length > 150)
        {
            return Result<MedicineDto>.Failure("Medicine name cannot exceed 150 characters.");
        }

        if (dto.GenericName != null && dto.GenericName.Trim().Length > 150)
        {
            return Result<MedicineDto>.Failure("Generic name cannot exceed 150 characters.");
        }

        if (dto.Form.Trim().Length > 50)
        {
            return Result<MedicineDto>.Failure("Dosage form cannot exceed 50 characters.");
        }

        if (dto.Strength != null && dto.Strength.Trim().Length > 50)
        {
            return Result<MedicineDto>.Failure("Strength cannot exceed 50 characters.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
            var medicine = await uow.Medicines.GetByIdAsync(dto.Id, cancellationToken);
            if (medicine == null)
            {
                return Result<MedicineDto>.Failure($"Medicine #{dto.Id} not found.");
            }

            if (!allowDuplicate)
            {
                var duplicates = await uow.Medicines.FindPotentialDuplicatesAsync(
                    dto.Name.Trim(),
                    dto.Form.Trim(),
                    dto.Strength?.Trim(),
                    excludeId: dto.Id,
                    cancellationToken: cancellationToken);

                if (duplicates.Count > 0)
                {
                    var dup = duplicates[0];
                    return Result<MedicineDto>.Failure(
                        $"DUPLICATE_WARNING: Another medicine with this formulation already exists in the catalog ('{dup.DisplayTitle}').");
                }
            }

            medicine.Name = dto.Name.Trim();
            medicine.GenericName = dto.GenericName?.Trim();
            medicine.Form = dto.Form.Trim();
            medicine.Strength = dto.Strength?.Trim() ?? string.Empty;
            medicine.IsActive = dto.IsActive;
            medicine.UpdatedAtUtc = _clock.UtcNow;
            medicine.RefreshSearchFields();

            await uow.Medicines.UpdateAsync(medicine, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Medicine #{Id} updated in catalog", dto.Id);
            return Result<MedicineDto>.Success(MapToDto(medicine));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update medicine #{Id}", dto.Id);
            return Result<MedicineDto>.Failure("Failed to update medicine.");
        }
    }

    public async Task<Result> DeleteMedicineAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var medicine = await uow.Medicines.GetByIdAsync(id, cancellationToken);
            if (medicine == null) return Result.Failure("Medicine not found.");

            var refCount = await uow.Medicines.GetPrescriptionReferenceCountAsync(id, cancellationToken);
            if (refCount > 0)
            {
                _logger.LogInformation(
                    "Medicine #{Id} ('{Name}') is referenced by {Count} prescription(s); deactivating to preserve historical prescription records.",
                    id, medicine.Name, refCount);
            }

            // Safe deletion rule: deactivates medicine formulation so it no longer appears in active searches
            // while preserving foreign key referential integrity and legal prescription snapshots
            medicine.IsActive = false;
            medicine.UpdatedAtUtc = _clock.UtcNow;

            await uow.Medicines.UpdateAsync(medicine, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete medicine #{Id}", id);
            return Result.Failure("Failed to delete medicine.");
        }
    }

    public async Task<Result> PurgeMedicineAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var medicine = await uow.Medicines.GetByIdAsync(id, cancellationToken);
            if (medicine == null) return Result.Failure("Medicine not found.");

            var refCount = await uow.Medicines.GetPrescriptionReferenceCountAsync(id, cancellationToken);
            if (refCount > 0)
            {
                return Result.Failure(
                    $"Cannot delete medicine '{medicine.DisplayTitle}': It is referenced by {refCount} historical prescription(s). You may deactivate it instead to remove it from future prescribing without altering medical history.");
            }

            await uow.Medicines.DeleteAsync(medicine, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Unreferenced medicine #{Id} ('{Name}') was purged from catalog", id, medicine.Name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge medicine #{Id}", id);
            return Result.Failure("Failed to purge medicine.");
        }
    }

    public async Task<MedicineUsageSummaryDto> GetMedicineUsageSummaryAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var medicine = await uow.Medicines.GetByIdAsync(id, cancellationToken);
        if (medicine == null)
        {
            return new MedicineUsageSummaryDto(id, string.Empty, false, 0, false);
        }

        var refCount = await uow.Medicines.GetPrescriptionReferenceCountAsync(id, cancellationToken);
        return new MedicineUsageSummaryDto(
            MedicineId: medicine.Id,
            MedicineName: medicine.DisplayTitle,
            IsReferencedInPrescriptions: refCount > 0,
            PrescriptionReferenceCount: refCount,
            CanHardDelete: refCount == 0
        );
    }

    private static MedicineDto MapToDto(Medicine m) =>
        new(m.Id, m.Name, m.GenericName, m.Form, m.Strength, m.IsActive, m.UsageCount);
}

public class DoctorService : IDoctorService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<DoctorService> _logger;

    public DoctorService(IUnitOfWorkFactory uowFactory, IClock clock, ILogger<DoctorService> logger)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var doc = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
        return doc is null ? null : MapToDto(doc);
    }

    public async Task<Result<DoctorDto>> CreateDoctorAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Result<DoctorDto>.Failure("Doctor name is required.");
        if (string.IsNullOrWhiteSpace(dto.Qualification)) return Result<DoctorDto>.Failure("Qualification is required.");
        if (string.IsNullOrWhiteSpace(dto.RegistrationNumber)) return Result<DoctorDto>.Failure("Registration number is required.");
        if (string.IsNullOrWhiteSpace(dto.Specialization)) return Result<DoctorDto>.Failure("Specialization is required.");
        if (string.IsNullOrWhiteSpace(dto.ClinicName)) return Result<DoctorDto>.Failure("Clinic name is required.");

        var titlePrefix = string.IsNullOrWhiteSpace(dto.TitlePrefix) ? "Dr." : dto.TitlePrefix.Trim();
        var registrationLabel = string.IsNullOrWhiteSpace(dto.RegistrationLabel) ? "Reg. No." : dto.RegistrationLabel.Trim();

        if (titlePrefix.Length > 20) return Result<DoctorDto>.Failure("Title prefix cannot exceed 20 characters.");
        if (registrationLabel.Length > 30) return Result<DoctorDto>.Failure("Registration label cannot exceed 30 characters.");

        if (dto.Name.Length > 150) return Result<DoctorDto>.Failure("Name cannot exceed 150 characters.");
        if (dto.Qualification.Length > 150) return Result<DoctorDto>.Failure("Qualification cannot exceed 150 characters.");
        if (dto.RegistrationNumber.Length > 50) return Result<DoctorDto>.Failure("Registration number cannot exceed 50 characters.");
        if (dto.Specialization.Length > 150) return Result<DoctorDto>.Failure("Specialization cannot exceed 150 characters.");
        if (dto.ClinicName.Length > 200) return Result<DoctorDto>.Failure("Clinic name cannot exceed 200 characters.");
        if (dto.Phone?.Length > 30) return Result<DoctorDto>.Failure("Phone cannot exceed 30 characters.");
        if (dto.ClinicPhone?.Length > 30) return Result<DoctorDto>.Failure("Clinic phone cannot exceed 30 characters.");
        if (dto.Email?.Length > 100) return Result<DoctorDto>.Failure("Email cannot exceed 100 characters.");
        if (dto.ClinicAddress?.Length > 300) return Result<DoctorDto>.Failure("Clinic address cannot exceed 300 characters.");
        if (dto.HeaderText?.Length > 500) return Result<DoctorDto>.Failure("Header text cannot exceed 500 characters.");
        if (dto.FooterText?.Length > 500) return Result<DoctorDto>.Failure("Footer text cannot exceed 500 characters.");

        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var digits = new string(dto.Phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return Result<DoctorDto>.Failure("Doctor phone must contain at least 7 digits.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ClinicPhone))
        {
            var digits = new string(dto.ClinicPhone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return Result<DoctorDto>.Failure("Clinic phone must contain at least 7 digits.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
            var activeDoc = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
            if (activeDoc != null)
            {
                return Result<DoctorDto>.Failure("An active doctor profile already exists.");
            }

            var doc = new Doctor
            {
                TitlePrefix = titlePrefix,
                Name = dto.Name.Trim(),
                Qualification = dto.Qualification.Trim(),
                RegistrationLabel = registrationLabel,
                RegistrationNumber = dto.RegistrationNumber.Trim(),
                Specialization = dto.Specialization.Trim(),
                Phone = dto.Phone?.Trim(),
                Email = dto.Email?.Trim(),
                ClinicName = dto.ClinicName.Trim(),
                ClinicAddress = dto.ClinicAddress?.Trim(),
                ClinicPhone = dto.ClinicPhone?.Trim(),
                HeaderText = dto.HeaderText?.Trim(),
                FooterText = dto.FooterText?.Trim(),
                IsActive = true,
                CreatedAtUtc = _clock.UtcNow
            };

            await uow.Doctors.AddAsync(doc, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Doctor profile created with Id {DoctorId}", doc.Id);
            return Result<DoctorDto>.Success(MapToDto(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create doctor profile");
            return Result<DoctorDto>.Failure("Unable to save doctor profile.");
        }
    }

    public async Task<Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0) return Result<DoctorDto>.Failure("Invalid doctor ID.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Result<DoctorDto>.Failure("Doctor name is required.");
        if (string.IsNullOrWhiteSpace(dto.Qualification)) return Result<DoctorDto>.Failure("Qualification is required.");
        if (string.IsNullOrWhiteSpace(dto.RegistrationNumber)) return Result<DoctorDto>.Failure("Registration number is required.");
        if (string.IsNullOrWhiteSpace(dto.Specialization)) return Result<DoctorDto>.Failure("Specialization is required.");
        if (string.IsNullOrWhiteSpace(dto.ClinicName)) return Result<DoctorDto>.Failure("Clinic name is required.");

        var titlePrefix = string.IsNullOrWhiteSpace(dto.TitlePrefix) ? "Dr." : dto.TitlePrefix.Trim();
        var registrationLabel = string.IsNullOrWhiteSpace(dto.RegistrationLabel) ? "Reg. No." : dto.RegistrationLabel.Trim();

        if (titlePrefix.Length > 20) return Result<DoctorDto>.Failure("Title prefix cannot exceed 20 characters.");
        if (registrationLabel.Length > 30) return Result<DoctorDto>.Failure("Registration label cannot exceed 30 characters.");

        if (dto.Name.Length > 150) return Result<DoctorDto>.Failure("Name cannot exceed 150 characters.");
        if (dto.Qualification.Length > 150) return Result<DoctorDto>.Failure("Qualification cannot exceed 150 characters.");
        if (dto.RegistrationNumber.Length > 50) return Result<DoctorDto>.Failure("Registration number cannot exceed 50 characters.");
        if (dto.Specialization.Length > 150) return Result<DoctorDto>.Failure("Specialization cannot exceed 150 characters.");
        if (dto.ClinicName.Length > 200) return Result<DoctorDto>.Failure("Clinic name cannot exceed 200 characters.");
        if (dto.Phone?.Length > 30) return Result<DoctorDto>.Failure("Phone cannot exceed 30 characters.");
        if (dto.ClinicPhone?.Length > 30) return Result<DoctorDto>.Failure("Clinic phone cannot exceed 30 characters.");
        if (dto.Email?.Length > 100) return Result<DoctorDto>.Failure("Email cannot exceed 100 characters.");
        if (dto.ClinicAddress?.Length > 300) return Result<DoctorDto>.Failure("Clinic address cannot exceed 300 characters.");
        if (dto.HeaderText?.Length > 500) return Result<DoctorDto>.Failure("Header text cannot exceed 500 characters.");
        if (dto.FooterText?.Length > 500) return Result<DoctorDto>.Failure("Footer text cannot exceed 500 characters.");

        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var digits = new string(dto.Phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return Result<DoctorDto>.Failure("Doctor phone must contain at least 7 digits.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ClinicPhone))
        {
            var digits = new string(dto.ClinicPhone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return Result<DoctorDto>.Failure("Clinic phone must contain at least 7 digits.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
            var doc = await uow.Doctors.GetByIdAsync(dto.Id, cancellationToken);
            if (doc == null)
            {
                return Result<DoctorDto>.Failure("Doctor profile not found.");
            }

            doc.TitlePrefix = titlePrefix;
            doc.Name = dto.Name.Trim();
            doc.Qualification = dto.Qualification.Trim();
            doc.RegistrationLabel = registrationLabel;
            doc.RegistrationNumber = dto.RegistrationNumber.Trim();
            doc.Specialization = dto.Specialization.Trim();
            doc.Phone = dto.Phone?.Trim();
            doc.Email = dto.Email?.Trim();
            doc.ClinicName = dto.ClinicName.Trim();
            doc.ClinicAddress = dto.ClinicAddress?.Trim();
            doc.ClinicPhone = dto.ClinicPhone?.Trim();
            doc.HeaderText = dto.HeaderText?.Trim();
            doc.FooterText = dto.FooterText?.Trim();
            doc.UpdatedAtUtc = _clock.UtcNow;

            await uow.Doctors.UpdateAsync(doc, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Doctor profile updated with Id {DoctorId}", doc.Id);
            return Result<DoctorDto>.Success(MapToDto(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update doctor profile");
            return Result<DoctorDto>.Failure("Unable to update doctor profile.");
        }
    }

    public async Task<Result<DoctorDto>> SwitchActiveDoctorAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        if (doctorId <= 0) return Result<DoctorDto>.Failure("Invalid doctor ID.");

        try
        {
            await using var uow = _uowFactory.Create();
            var target = await uow.Doctors.GetByIdAsync(doctorId, cancellationToken);
            if (target == null)
            {
                return Result<DoctorDto>.Failure("Doctor profile not found.");
            }

            var active = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
            if (active != null && active.Id != doctorId)
            {
                active.IsActive = false;
                active.UpdatedAtUtc = _clock.UtcNow;
                await uow.Doctors.UpdateAsync(active, cancellationToken);
            }

            target.IsActive = true;
            target.UpdatedAtUtc = _clock.UtcNow;
            await uow.Doctors.UpdateAsync(target, cancellationToken);

            await uow.CommitAsync(cancellationToken);
            _logger.LogInformation("Switched active doctor to Id {DoctorId}", target.Id);
            return Result<DoctorDto>.Success(MapToDto(target));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch active doctor to Id {DoctorId}", doctorId);
            return Result<DoctorDto>.Failure("Unable to switch active doctor.");
        }
    }

    private static DoctorDto MapToDto(Doctor d) =>
        new(d.Id, d.Name, d.Qualification, d.RegistrationNumber, d.Specialization, d.Phone, d.Email, d.ClinicName, d.ClinicAddress, d.ClinicPhone, d.HeaderText, d.FooterText, d.TitlePrefix, d.RegistrationLabel);
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;

    public DashboardService(IUnitOfWorkFactory uowFactory, IClock clock)
    {
        _uowFactory = uowFactory;
        _clock = clock;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();

        var totalPatients = await uow.Patients.GetTotalCountAsync(showArchived: false, cancellationToken: cancellationToken);
        var totalPrescriptions = await uow.Prescriptions.CountAsync(cancellationToken: cancellationToken);
        var prescriptionsToday = await uow.Prescriptions.GetCountForDateAsync(_clock.Today, cancellationToken);
        var totalMedicines = await uow.Medicines.CountAsync(m => m.IsActive, cancellationToken);

        var recentPrescriptions = await uow.Prescriptions.GetRecentPrescriptionsAsync(8, cancellationToken);
        var recentPatients = await uow.Patients.GetRecentPatientsAsync(8, cancellationToken);

        var rxDtos = recentPrescriptions.Select(p => new PrescriptionSummaryDto(
            p.Id,
            p.PrescriptionNumber,
            p.PatientId,
            p.PatientSnapshot.Name,
            p.PatientSnapshot.AgeText,
            p.PatientSnapshot.Gender,
            p.PrescriptionDate,
            p.Status,
            p.Items?.Count ?? 0,
            p.FollowUpDate,
            p.AmendmentNumber
        )).ToList();

        var patientDtos = recentPatients.Select(p => new PatientDto(
            p.Id,
            p.RecordNumber,
            p.Name,
            p.DateOfBirth,
            p.CalculateAge(_clock.Today),
            p.Gender,
            p.Phone,
            p.Address,
            p.MedicalHistoryNotes,
            p.KnownAllergies,
            p.CreatedAtUtc,
            p.LastVisitDate,
            p.IsArchived,
            p.ArchivedAtUtc
        )).ToList();

        return new DashboardStatsDto(
            totalPatients,
            prescriptionsToday,
            totalPrescriptions,
            totalMedicines,
            rxDtos,
            patientDtos
        );
    }
}
