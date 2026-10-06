using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IPatientService
{
    Task<PagedResult<PatientDto>> GetPatientsPagedAsync(int pageNumber, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PatientDto>> GetRecentPatientsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<PatientDto?> GetPatientByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<PatientDto>> CreatePatientAsync(CreatePatientDto dto, CancellationToken cancellationToken = default);
    Task<Result<PatientDto>> UpdatePatientAsync(UpdatePatientDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeletePatientAsync(int id, CancellationToken cancellationToken = default);
}
