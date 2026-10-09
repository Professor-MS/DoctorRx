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

    public MedicineService(IUnitOfWorkFactory uowFactory, IClock clock, ILogger<MedicineService> logger)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _logger = logger;
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

    public async Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetMedicinesPagedAsync(1, maxResults, cancellationToken);
        }

        await using var uow = _uowFactory.Create();
        var list = await uow.Medicines.SearchAsync(query, maxResults, cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var medicine = await uow.Medicines.GetByIdAsync(id, cancellationToken);
        return medicine is null ? null : MapToDto(medicine);
    }

    public async Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<MedicineDto>.Failure("Medicine brand or name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Form))
        {
            return Result<MedicineDto>.Failure("Dosage form is required.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
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

            _logger.LogInformation("Medicine created: {Id}", medicine.Id);
            return Result<MedicineDto>.Success(MapToDto(medicine));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create medicine");
            return Result<MedicineDto>.Failure("Failed to add medicine to catalog.");
        }
    }

    public async Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, CancellationToken cancellationToken = default)
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

        try
        {
            await using var uow = _uowFactory.Create();
            var medicine = await uow.Medicines.GetByIdAsync(dto.Id, cancellationToken);
            if (medicine == null)
            {
                return Result<MedicineDto>.Failure($"Medicine #{dto.Id} not found.");
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

    private static MedicineDto MapToDto(Medicine m) =>
        new(m.Id, m.Name, m.GenericName, m.Form, m.Strength, m.IsActive);
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

        try
        {
            await using var uow = _uowFactory.Create();
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
