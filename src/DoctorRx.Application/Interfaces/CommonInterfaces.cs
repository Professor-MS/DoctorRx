using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IMedicineService
{
    Task<IReadOnlyList<MedicineDto>> GetMedicinesPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<MedicineDto?> GetMedicineByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<MedicineDto>> CreateMedicineAsync(CreateMedicineDto dto, CancellationToken cancellationToken = default);
    Task<Result<MedicineDto>> UpdateMedicineAsync(UpdateMedicineDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteMedicineAsync(int id, CancellationToken cancellationToken = default);
}

public interface IDoctorService
{
    Task<DoctorDto?> GetActiveDoctorAsync(CancellationToken cancellationToken = default);
    Task<Result<DoctorDto>> CreateDoctorAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default);
    Task<Result<DoctorDto>> UpdateDoctorAsync(UpdateDoctorDto dto, CancellationToken cancellationToken = default);
    Task<Result<DoctorDto>> SwitchActiveDoctorAsync(int doctorId, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public interface IPrescriptionComposerValidator
{
    ComposerValidationResult Validate(PrescriptionComposerState state);
}
