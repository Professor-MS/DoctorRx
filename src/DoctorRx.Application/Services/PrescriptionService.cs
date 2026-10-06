using System;
using System.Collections.Generic;
using System.Linq;
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

public class PrescriptionService : IPrescriptionService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly ILogger<PrescriptionService> _logger;

    public PrescriptionService(IUnitOfWorkFactory uowFactory, ILogger<PrescriptionService> logger)
    {
        _uowFactory = uowFactory;
        _logger = logger;
    }

    public async Task<PrescriptionDetailDto?> GetPrescriptionByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var rx = await uow.Prescriptions.GetDetailedAsync(id, cancellationToken);
        return rx is null ? null : MapToDetailDto(rx);
    }

    public async Task<IReadOnlyList<PrescriptionSummaryDto>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var list = await uow.Prescriptions.GetRecentPrescriptionsAsync(count, cancellationToken);
        return list.Select(MapToSummaryDto).ToList();
    }

    public async Task<IReadOnlyList<PrescriptionSummaryDto>> GetPrescriptionsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var list = await uow.Prescriptions.GetByPatientIdAsync(patientId, cancellationToken);
        return list.Select(MapToSummaryDto).ToList();
    }

    public async Task<Result<PrescriptionDetailDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.PatientId <= 0)
        {
            return Result<PrescriptionDetailDto>.Failure("A valid patient must be selected.");
        }

        await using var uow = _uowFactory.Create();

        if (dto.DoctorId <= 0)
        {
            var activeDoctor = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
            if (activeDoctor == null)
            {
                return Result<PrescriptionDetailDto>.Failure("No active doctor profile found in system settings.");
            }
            dto.DoctorId = activeDoctor.Id;
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return Result<PrescriptionDetailDto>.Failure("At least one prescribed medicine is required.");
        }

        try
        {
            var patient = await uow.Patients.GetByIdAsync(dto.PatientId, cancellationToken);
            if (patient == null)
            {
                return Result<PrescriptionDetailDto>.Failure("Selected patient does not exist.");
            }

            var doctor = await uow.Doctors.GetByIdAsync(dto.DoctorId, cancellationToken);
            if (doctor == null)
            {
                return Result<PrescriptionDetailDto>.Failure("Selected doctor profile was not found.");
            }

            var prescriptionNumber = await uow.Prescriptions.GenerateNextPrescriptionNumberAsync(cancellationToken);

            var prescription = new Prescription
            {
                PrescriptionNumber = prescriptionNumber,
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                PrescriptionDate = dto.PrescriptionDate,
                ChiefComplaints = dto.ChiefComplaints?.Trim(),
                BloodPressure = dto.BloodPressure?.Trim(),
                PulseRate = dto.PulseRate?.Trim(),
                Temperature = dto.Temperature?.Trim(),
                WeightKg = dto.WeightKg?.Trim(),
                ClinicalNotes = dto.ClinicalNotes?.Trim(),
                GeneralAdvice = dto.GeneralAdvice?.Trim(),
                FollowUpDate = dto.FollowUpDate,
                Status = PrescriptionStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow
            };

            int order = 1;
            foreach (var itemDto in dto.Items)
            {
                if (string.IsNullOrWhiteSpace(itemDto.MedicineName))
                {
                    continue;
                }

                var item = new PrescriptionMedicine
                {
                    MedicineId = itemDto.MedicineId,
                    MedicineName = itemDto.MedicineName.Trim(),
                    GenericName = itemDto.GenericName?.Trim(),
                    Form = string.IsNullOrWhiteSpace(itemDto.Form) ? "Tablet" : itemDto.Form.Trim(),
                    Strength = itemDto.Strength?.Trim() ?? string.Empty,
                    Dose = itemDto.Dose?.Trim() ?? string.Empty,
                    Frequency = itemDto.Frequency?.Trim() ?? string.Empty,
                    Timing = itemDto.Timing?.Trim(),
                    MealRelation = itemDto.MealRelation,
                    CustomMealRelationText = itemDto.CustomMealRelationText?.Trim(),
                    Route = string.IsNullOrWhiteSpace(itemDto.Route) ? "Oral" : itemDto.Route.Trim(),
                    Duration = itemDto.Duration?.Trim() ?? string.Empty,
                    Instructions = itemDto.Instructions?.Trim(),
                    SortOrder = order++
                };

                prescription.AddMedicine(item);
            }

            await uow.Prescriptions.AddAsync(prescription, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription created successfully with ID {PrescriptionId}", prescription.Id);

            var saved = await uow.Prescriptions.GetDetailedAsync(prescription.Id, cancellationToken);
            return Result<PrescriptionDetailDto>.Success(MapToDetailDto(saved!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create prescription");
            return Result<PrescriptionDetailDto>.Failure("Unable to save prescription. Please review the details and try again.");
        }
    }

    public async Task<Result> FinalizePrescriptionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var rx = await uow.Prescriptions.GetByIdAsync(id, cancellationToken);
            if (rx == null)
            {
                return Result.Failure($"Prescription #{id} not found.");
            }

            rx.FinalizePrescription();
            rx.UpdatedAtUtc = DateTime.UtcNow;

            await uow.Prescriptions.UpdateAsync(rx, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription #{PrescriptionId} finalized", rx.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize prescription #{Id}", id);
            return Result.Failure("Failed to finalize prescription.");
        }
    }

    public async Task<Result> CancelPrescriptionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            var rx = await uow.Prescriptions.GetByIdAsync(id, cancellationToken);
            if (rx == null)
            {
                return Result.Failure($"Prescription #{id} not found.");
            }

            rx.Status = PrescriptionStatus.Cancelled;
            rx.UpdatedAtUtc = DateTime.UtcNow;

            await uow.Prescriptions.UpdateAsync(rx, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription #{PrescriptionId} cancelled", rx.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel prescription #{Id}", id);
            return Result.Failure("Failed to cancel prescription.");
        }
    }

    private static PrescriptionSummaryDto MapToSummaryDto(Prescription p)
    {
        return new PrescriptionSummaryDto(
            p.Id,
            p.PrescriptionNumber,
            p.PatientId,
            p.Patient?.Name ?? "Unknown Patient",
            p.Patient?.CalculatedAge ?? 0,
            p.Patient?.Gender ?? Gender.NotSpecified,
            p.PrescriptionDate,
            p.Status,
            p.Items?.Count ?? 0,
            p.FollowUpDate
        );
    }

    private static PrescriptionDetailDto MapToDetailDto(Prescription p)
    {
        var patientDto = new PatientDto(
            p.Patient!.Id,
            p.Patient.Name,
            p.Patient.DateOfBirth,
            p.Patient.CalculatedAge,
            p.Patient.Gender,
            p.Patient.Phone,
            p.Patient.Address,
            p.Patient.MedicalHistoryNotes,
            p.Patient.KnownAllergies,
            p.Patient.CreatedAtUtc,
            p.PrescriptionDate
        );

        var doctorDto = new DoctorDto(
            p.Doctor!.Id,
            p.Doctor.Name,
            p.Doctor.Qualification,
            p.Doctor.RegistrationNumber,
            p.Doctor.Specialization,
            p.Doctor.Phone,
            p.Doctor.Email,
            p.Doctor.ClinicName,
            p.Doctor.ClinicAddress,
            p.Doctor.ClinicPhone,
            p.Doctor.HeaderText,
            p.Doctor.FooterText
        );

        var items = p.Items.OrderBy(i => i.SortOrder).Select(i => new PrescriptionMedicineDto(
            i.Id,
            i.MedicineId,
            i.MedicineName,
            i.GenericName,
            i.Form,
            i.Strength,
            i.Dose,
            i.Frequency,
            i.Timing,
            i.MealRelation,
            i.CustomMealRelationText,
            i.Route,
            i.Duration,
            i.Instructions,
            i.SortOrder
        )).ToList();

        return new PrescriptionDetailDto(
            p.Id,
            p.PrescriptionNumber,
            p.PatientId,
            patientDto,
            p.DoctorId,
            doctorDto,
            p.PrescriptionDate,
            p.ChiefComplaints,
            p.BloodPressure,
            p.PulseRate,
            p.Temperature,
            p.WeightKg,
            p.ClinicalNotes,
            p.GeneralAdvice,
            p.FollowUpDate,
            p.Status,
            items,
            p.CreatedAtUtc
        );
    }
}
