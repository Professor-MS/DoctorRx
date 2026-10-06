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
using DoctorRx.Domain.Exceptions;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<PrescriptionService> _logger;

    public PrescriptionService(IUnitOfWorkFactory uowFactory, IClock clock, ILogger<PrescriptionService> logger)
    {
        _uowFactory = uowFactory;
        _clock = clock;
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

    public async Task<Result<PrescriptionDetailDto>> FinalizePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.PatientId <= 0)
        {
            return Result<PrescriptionDetailDto>.Failure("A valid patient must be selected.");
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return Result<PrescriptionDetailDto>.Failure("Prescription must contain at least one prescribed medicine.");
        }

        // Strict medicine row validation - no silent skips and no silent fallbacks
        for (int i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            int rowNumber = i + 1;

            if (string.IsNullOrWhiteSpace(item.MedicineName))
            {
                return Result<PrescriptionDetailDto>.Failure($"Medicine line #{rowNumber}: Medicine name cannot be blank.");
            }
            if (string.IsNullOrWhiteSpace(item.Dose))
            {
                return Result<PrescriptionDetailDto>.Failure($"Medicine line #{rowNumber} ('{item.MedicineName}'): Dose is required.");
            }
            if (string.IsNullOrWhiteSpace(item.Frequency))
            {
                return Result<PrescriptionDetailDto>.Failure($"Medicine line #{rowNumber} ('{item.MedicineName}'): Frequency is required.");
            }
            if (string.IsNullOrWhiteSpace(item.Form))
            {
                return Result<PrescriptionDetailDto>.Failure($"Medicine line #{rowNumber} ('{item.MedicineName}'): Formulation form is required.");
            }
        }

        try
        {
            await using var uow = _uowFactory.Create();
            await using var tx = await uow.BeginWriteTransactionAsync(cancellationToken);

            var patient = await uow.Patients.GetByIdAsync(dto.PatientId, cancellationToken);
            if (patient == null)
            {
                return Result<PrescriptionDetailDto>.Failure("Selected patient was not found.");
            }

            var doctorId = dto.DoctorId;
            Doctor? doctor = null;
            if (doctorId > 0)
            {
                doctor = await uow.Doctors.GetByIdAsync(doctorId, cancellationToken);
            }
            if (doctor == null)
            {
                doctor = await uow.Doctors.GetActiveDoctorAsync(cancellationToken);
                if (doctor == null)
                {
                    return Result<PrescriptionDetailDto>.Failure("No active doctor profile found in system settings.");
                }
                doctorId = doctor.Id;
            }

            var prescriptionDate = dto.PrescriptionDate == default ? _clock.Today : dto.PrescriptionDate;
            var doctorSnapshot = doctor.ToSnapshot();
            var patientSnapshot = patient.ToSnapshot(prescriptionDate);

            var prescriptionNumber = await uow.Prescriptions.GenerateNextPrescriptionNumberAsync(cancellationToken);

            var rx = Prescription.CreateFinalized(
                prescriptionNumber: prescriptionNumber,
                patientId: patient.Id,
                doctorId: doctor.Id,
                prescriptionDate: prescriptionDate,
                doctorSnapshot: doctorSnapshot,
                patientSnapshot: patientSnapshot,
                finalizedAtUtc: _clock.UtcNow,
                chiefComplaints: dto.ChiefComplaints,
                bloodPressure: dto.BloodPressure,
                pulseRate: dto.PulseRate,
                temperature: dto.Temperature,
                weightKg: dto.WeightKg,
                clinicalNotes: dto.ClinicalNotes,
                generalAdvice: dto.GeneralAdvice,
                followUpDate: dto.FollowUpDate
            );

            foreach (var itemDto in dto.Items)
            {
                rx.AddMedicine(new PrescriptionMedicine
                {
                    MedicineId = itemDto.MedicineId,
                    MedicineName = itemDto.MedicineName.Trim(),
                    GenericName = itemDto.GenericName?.Trim(),
                    Form = itemDto.Form.Trim(),
                    Strength = itemDto.Strength?.Trim() ?? string.Empty,
                    Dose = itemDto.Dose.Trim(),
                    Frequency = itemDto.Frequency.Trim(),
                    Timing = itemDto.Timing?.Trim(),
                    MealRelation = itemDto.MealRelation,
                    CustomMealRelationText = itemDto.CustomMealRelationText?.Trim(),
                    WithWhat = itemDto.WithWhat?.Trim(),
                    Route = itemDto.Route?.Trim() ?? string.Empty,
                    Duration = itemDto.Duration?.Trim() ?? string.Empty,
                    Instructions = itemDto.Instructions?.Trim()
                });
            }

            await uow.Prescriptions.AddAsync(rx, cancellationToken);

            // Update patient's last visit date on finalization (Amendment 10)
            patient.LastVisitDate = prescriptionDate;
            patient.UpdatedAtUtc = _clock.UtcNow;
            await uow.Patients.UpdateAsync(patient, cancellationToken);

            await uow.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription #{PrescriptionId} finalized successfully", rx.Id);

            var saved = await uow.Prescriptions.GetDetailedAsync(rx.Id, cancellationToken);
            return Result<PrescriptionDetailDto>.Success(MapToDetailDto(saved!));
        }
        catch (DomainRuleException dex)
        {
            return Result<PrescriptionDetailDto>.Failure(dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize prescription");
            return Result<PrescriptionDetailDto>.Failure("Unable to finalize prescription due to an unexpected storage error.");
        }
    }

    public async Task<Result<PrescriptionDetailDto>> AmendPrescriptionAsync(int originalId, CreatePrescriptionDto newContent, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var uow = _uowFactory.Create();
            await using var tx = await uow.BeginWriteTransactionAsync(cancellationToken);

            var original = await uow.Prescriptions.GetDetailedAsync(originalId, cancellationToken);
            if (original == null)
            {
                return Result<PrescriptionDetailDto>.Failure($"Original prescription #{originalId} not found.");
            }

            if (original.Status != PrescriptionStatus.Finalized)
            {
                return Result<PrescriptionDetailDto>.Failure($"Cannot amend prescription with status {original.Status}. Only finalized prescriptions can be amended.");
            }

            // Create amendment number format: RX-YYYYMMDD-####-A1
            var nextAmendmentNumber = original.AmendmentNumber + 1;
            var baseNumber = original.PrescriptionNumber.Contains("-A")
                ? original.PrescriptionNumber.Substring(0, original.PrescriptionNumber.LastIndexOf("-A", StringComparison.Ordinal))
                : original.PrescriptionNumber;
            var amendedNumber = $"{baseNumber}-A{nextAmendmentNumber}";

            // Mark original as superseded
            original.MarkSuperseded(_clock.UtcNow);
            await uow.Prescriptions.UpdateAsync(original, cancellationToken);

            // Create new prescription linked to parent
            var patient = await uow.Patients.GetByIdAsync(original.PatientId, cancellationToken);
            var doctor = await uow.Doctors.GetByIdAsync(original.DoctorId, cancellationToken);
            if (patient == null || doctor == null)
            {
                return Result<PrescriptionDetailDto>.Failure("Associated patient or doctor profile not found.");
            }

            var prescriptionDate = newContent.PrescriptionDate == default ? _clock.Today : newContent.PrescriptionDate;
            var amendedRx = Prescription.CreateFinalized(
                prescriptionNumber: amendedNumber,
                patientId: original.PatientId,
                doctorId: original.DoctorId,
                prescriptionDate: prescriptionDate,
                doctorSnapshot: doctor.ToSnapshot(),
                patientSnapshot: patient.ToSnapshot(prescriptionDate),
                finalizedAtUtc: _clock.UtcNow,
                parentPrescriptionId: original.Id,
                amendmentNumber: nextAmendmentNumber,
                chiefComplaints: newContent.ChiefComplaints,
                bloodPressure: newContent.BloodPressure,
                pulseRate: newContent.PulseRate,
                temperature: newContent.Temperature,
                weightKg: newContent.WeightKg,
                clinicalNotes: newContent.ClinicalNotes,
                generalAdvice: newContent.GeneralAdvice,
                followUpDate: newContent.FollowUpDate
            );

            foreach (var itemDto in newContent.Items)
            {
                amendedRx.AddMedicine(new PrescriptionMedicine
                {
                    MedicineId = itemDto.MedicineId,
                    MedicineName = itemDto.MedicineName.Trim(),
                    GenericName = itemDto.GenericName?.Trim(),
                    Form = itemDto.Form.Trim(),
                    Strength = itemDto.Strength?.Trim() ?? string.Empty,
                    Dose = itemDto.Dose.Trim(),
                    Frequency = itemDto.Frequency.Trim(),
                    Timing = itemDto.Timing?.Trim(),
                    MealRelation = itemDto.MealRelation,
                    CustomMealRelationText = itemDto.CustomMealRelationText?.Trim(),
                    WithWhat = itemDto.WithWhat?.Trim(),
                    Route = itemDto.Route?.Trim() ?? string.Empty,
                    Duration = itemDto.Duration?.Trim() ?? string.Empty,
                    Instructions = itemDto.Instructions?.Trim()
                });
            }

            await uow.Prescriptions.AddAsync(amendedRx, cancellationToken);

            patient.LastVisitDate = prescriptionDate;
            patient.UpdatedAtUtc = _clock.UtcNow;
            await uow.Patients.UpdateAsync(patient, cancellationToken);

            await uow.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription #{OriginalId} superseded by amendment #{AmendedId}", original.Id, amendedRx.Id);

            var saved = await uow.Prescriptions.GetDetailedAsync(amendedRx.Id, cancellationToken);
            return Result<PrescriptionDetailDto>.Success(MapToDetailDto(saved!));
        }
        catch (DomainRuleException dex)
        {
            return Result<PrescriptionDetailDto>.Failure(dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to amend prescription #{OriginalId}", originalId);
            return Result<PrescriptionDetailDto>.Failure("Unable to amend prescription.");
        }
    }

    public async Task<Result> CancelPrescriptionAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure("A cancellation reason is required.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
            await using var tx = await uow.BeginWriteTransactionAsync(cancellationToken);

            var rx = await uow.Prescriptions.GetDetailedAsync(id, cancellationToken);
            if (rx == null)
            {
                return Result.Failure($"Prescription #{id} not found.");
            }

            rx.Cancel(reason, _clock.UtcNow);
            await uow.Prescriptions.UpdateAsync(rx, cancellationToken);

            // Recalculate patient's last visit date on cancel (Amendment 10)
            var patient = await uow.Patients.GetWithPrescriptionsAsync(rx.PatientId, cancellationToken);
            if (patient != null)
            {
                var remainingActive = patient.Prescriptions
                    .Where(p => p.Id != id && p.Status == PrescriptionStatus.Finalized)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .FirstOrDefault();

                patient.LastVisitDate = remainingActive?.PrescriptionDate;
                patient.UpdatedAtUtc = _clock.UtcNow;
                await uow.Patients.UpdateAsync(patient, cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Prescription #{PrescriptionId} cancelled successfully", rx.Id);
            return Result.Success();
        }
        catch (DomainRuleException dex)
        {
            return Result.Failure(dex.Message);
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
            p.PatientSnapshot.Name,
            p.PatientSnapshot.AgeText,
            p.PatientSnapshot.Gender,
            p.PrescriptionDate,
            p.Status,
            p.Items?.Count ?? 0,
            p.FollowUpDate,
            p.AmendmentNumber
        );
    }

    private static PrescriptionDetailDto MapToDetailDto(Prescription p)
    {
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
            i.WithWhat,
            i.Route,
            i.Duration,
            i.Instructions,
            i.SortOrder
        )).ToList();

        return new PrescriptionDetailDto(
            p.Id,
            p.PrescriptionNumber,
            p.PatientId,
            p.PatientSnapshot,
            p.DoctorId,
            p.DoctorSnapshot,
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
            p.FinalizedAtUtc,
            p.CancelledAtUtc,
            p.CancellationReason,
            p.ParentPrescriptionId,
            p.AmendmentNumber,
            p.Version,
            items
        );
    }
}
