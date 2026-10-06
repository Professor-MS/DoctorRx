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

public class PatientService : IPatientService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly ILogger<PatientService> _logger;

    public PatientService(IUnitOfWorkFactory uowFactory, ILogger<PatientService> logger)
    {
        _uowFactory = uowFactory;
        _logger = logger;
    }

    public async Task<PagedResult<PatientDto>> GetPatientsPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 50;

        await using var uow = _uowFactory.Create();
        var totalCount = await uow.Patients.GetTotalCountAsync(cancellationToken);
        var patients = await uow.Patients.GetPagedAsync(pageNumber, pageSize, cancellationToken);
        var dtos = patients.Select(MapToDto).ToList();

        return new PagedResult<PatientDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await using var uow = _uowFactory.Create();
            var paged = await uow.Patients.GetPagedAsync(1, maxResults, cancellationToken);
            return paged.Select(MapToDto).ToList();
        }

        await using var uowSearch = _uowFactory.Create();
        var patients = await uowSearch.Patients.SearchAsync(query, maxResults, cancellationToken);
        return patients.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<PatientDto>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var patients = await uow.Patients.GetRecentPatientsAsync(count, cancellationToken);
        return patients.Select(MapToDto).ToList();
    }

    public async Task<PatientDto?> GetPatientByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var uow = _uowFactory.Create();
        var patient = await uow.Patients.GetWithPrescriptionsAsync(id, cancellationToken);
        return patient is null ? null : MapToDto(patient);
    }

    public async Task<Result<PatientDto>> CreatePatientAsync(CreatePatientDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<PatientDto>.Failure("Patient name is required.");
        }

        if (dto.Age is < 0 or > 130)
        {
            return Result<PatientDto>.Failure("Age must be between 0 and 130.");
        }

        try
        {
            await using var uow = _uowFactory.Create();
            var patient = new Patient
            {
                Name = dto.Name.Trim(),
                DateOfBirth = dto.DateOfBirth,
                Age = dto.Age,
                Gender = dto.Gender,
                Phone = dto.Phone?.Trim(),
                Address = dto.Address?.Trim(),
                MedicalHistoryNotes = dto.MedicalHistoryNotes?.Trim(),
                KnownAllergies = dto.KnownAllergies?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await uow.Patients.AddAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient created successfully with Id {PatientId}", patient.Id);
            return Result<PatientDto>.Success(MapToDto(patient));
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

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<PatientDto>.Failure("Patient name is required.");
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
            patient.Gender = dto.Gender;
            patient.Phone = dto.Phone?.Trim();
            patient.Address = dto.Address?.Trim();
            patient.MedicalHistoryNotes = dto.MedicalHistoryNotes?.Trim();
            patient.KnownAllergies = dto.KnownAllergies?.Trim();
            patient.UpdatedAtUtc = DateTime.UtcNow;

            await uow.Patients.UpdateAsync(patient, cancellationToken);
            await uow.CommitAsync(cancellationToken);

            _logger.LogInformation("Patient updated with Id {PatientId}", patient.Id);
            return Result<PatientDto>.Success(MapToDto(patient));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update patient Id {PatientId}", dto.Id);
            return Result<PatientDto>.Failure("Unable to update patient. Please check the information and try again.");
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

    private static PatientDto MapToDto(Patient p)
    {
        var lastVisit = p.Prescriptions?.OrderByDescending(x => x.PrescriptionDate).FirstOrDefault()?.PrescriptionDate;
        return new PatientDto(
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
            lastVisit
        );
    }
}
