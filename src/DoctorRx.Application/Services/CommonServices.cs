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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MedicineService> _logger;

    public MedicineService(IUnitOfWork unitOfWork, ILogger<MedicineService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MedicineDto>> GetAllMedicinesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.Medicines.ListAllAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllMedicinesAsync(cancellationToken);
        }

        var list = await _unitOfWork.Medicines.SearchAsync(query, 50, cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var medicine = await _unitOfWork.Medicines.GetByIdAsync(id, cancellationToken);
        return medicine is null ? null : MapToDto(medicine);
    }

    public async Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<MedicineDto>.Failure("Medicine brand or name is required.");
        }

        try
        {
            var medicine = new Medicine
            {
                Name = dto.Name.Trim(),
                GenericName = dto.GenericName?.Trim(),
                Form = string.IsNullOrWhiteSpace(dto.Form) ? "Tablet" : dto.Form.Trim(),
                Strength = dto.Strength?.Trim() ?? string.Empty,
                DefaultDose = dto.DefaultDose?.Trim(),
                DefaultFrequency = dto.DefaultFrequency?.Trim(),
                DefaultRoute = string.IsNullOrWhiteSpace(dto.DefaultRoute) ? "Oral" : dto.DefaultRoute.Trim(),
                DefaultInstructions = dto.DefaultInstructions?.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.Medicines.AddAsync(medicine, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Medicine created: {Name}", medicine.Name);
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

        try
        {
            var medicine = await _unitOfWork.Medicines.GetByIdAsync(dto.Id, cancellationToken);
            if (medicine == null)
            {
                return Result<MedicineDto>.Failure($"Medicine #{dto.Id} not found.");
            }

            medicine.Name = dto.Name.Trim();
            medicine.GenericName = dto.GenericName?.Trim();
            medicine.Form = dto.Form.Trim();
            medicine.Strength = dto.Strength.Trim();
            medicine.DefaultDose = dto.DefaultDose?.Trim();
            medicine.DefaultFrequency = dto.DefaultFrequency?.Trim();
            medicine.DefaultRoute = dto.DefaultRoute?.Trim() ?? "Oral";
            medicine.DefaultInstructions = dto.DefaultInstructions?.Trim();
            medicine.IsActive = dto.IsActive;
            medicine.UpdatedAtUtc = DateTime.UtcNow;

            await _unitOfWork.Medicines.UpdateAsync(medicine, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

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
            var medicine = await _unitOfWork.Medicines.GetByIdAsync(id, cancellationToken);
            if (medicine == null) return Result.Failure("Medicine not found.");

            medicine.IsActive = false; // Soft-delete by setting inactive
            medicine.UpdatedAtUtc = DateTime.UtcNow;

            await _unitOfWork.Medicines.UpdateAsync(medicine, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete medicine #{Id}", id);
            return Result.Failure("Failed to delete medicine.");
        }
    }

    private static MedicineDto MapToDto(Medicine m) =>
        new(m.Id, m.Name, m.GenericName, m.Form, m.Strength, m.DefaultDose, m.DefaultFrequency, m.DefaultRoute, m.DefaultInstructions, m.IsActive);
}

public class DoctorService : IDoctorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DoctorService> _logger;

    public DoctorService(IUnitOfWork unitOfWork, ILogger<DoctorService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default)
    {
        var doc = await _unitOfWork.Doctors.GetActiveDoctorAsync(cancellationToken);
        return doc is null ? null : MapToDto(doc);
    }

    public async Task<Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await _unitOfWork.Doctors.GetByIdAsync(dto.Id, cancellationToken);
            if (doc == null)
            {
                return Result<DoctorDto>.Failure("Doctor profile not found.");
            }

            doc.Name = dto.Name.Trim();
            doc.Qualification = dto.Qualification.Trim();
            doc.RegistrationNumber = dto.RegistrationNumber.Trim();
            doc.Specialization = dto.Specialization.Trim();
            doc.Phone = dto.Phone?.Trim();
            doc.Email = dto.Email?.Trim();
            doc.ClinicName = dto.ClinicName.Trim();
            doc.ClinicAddress = dto.ClinicAddress?.Trim();
            doc.ClinicPhone = dto.ClinicPhone?.Trim();
            doc.HeaderText = dto.HeaderText?.Trim();
            doc.FooterText = dto.FooterText?.Trim();
            doc.UpdatedAtUtc = DateTime.UtcNow;

            await _unitOfWork.Doctors.UpdateAsync(doc, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return Result<DoctorDto>.Success(MapToDto(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update doctor profile");
            return Result<DoctorDto>.Failure("Unable to update doctor profile.");
        }
    }

    private static DoctorDto MapToDto(Doctor d) =>
        new(d.Id, d.Name, d.Qualification, d.RegistrationNumber, d.Specialization, d.Phone, d.Email, d.ClinicName, d.ClinicAddress, d.ClinicPhone, d.HeaderText, d.FooterText);
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalPatients = await _unitOfWork.Patients.CountAsync(cancellationToken: cancellationToken);
        var totalPrescriptions = await _unitOfWork.Prescriptions.CountAsync(cancellationToken: cancellationToken);
        var prescriptionsToday = await _unitOfWork.Prescriptions.GetCountForDateAsync(DateTime.Today, cancellationToken);
        var totalMedicines = await _unitOfWork.Medicines.CountAsync(m => m.IsActive, cancellationToken);

        var recentPrescriptions = await _unitOfWork.Prescriptions.GetRecentPrescriptionsAsync(8, cancellationToken);
        var recentPatients = await _unitOfWork.Patients.GetRecentPatientsAsync(8, cancellationToken);

        var rxDtos = recentPrescriptions.Select(p => new PrescriptionSummaryDto(
            p.Id,
            p.PrescriptionNumber,
            p.PatientId,
            p.Patient?.Name ?? "Unknown Patient",
            p.Patient?.CalculatedAge ?? 0,
            p.Patient?.Gender ?? Domain.Enums.Gender.NotSpecified,
            p.PrescriptionDate,
            p.Status,
            p.Items?.Count ?? 0,
            p.FollowUpDate
        )).ToList();

        var patientDtos = recentPatients.Select(p => new PatientDto(
            p.Id,
            p.Name,
            p.DateOfBirth,
            p.CalculatedAge,
            p.Gender,
            p.Phone,
            p.Address,
            p.MedicalHistoryNotes,
            p.KnownAllergies,
            p.CreatedAtUtc,
            p.Prescriptions?.OrderByDescending(x => x.PrescriptionDate).FirstOrDefault()?.PrescriptionDate
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
