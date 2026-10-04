using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IPrescriptionService
{
    Task<PrescriptionDetailDto?> GetPrescriptionByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrescriptionSummaryDto>> GetRecentPrescriptionsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrescriptionSummaryDto>> GetPrescriptionsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);
    Task<Result<PrescriptionDetailDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, CancellationToken cancellationToken = default);
    Task<Result> FinalizePrescriptionAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> CancelPrescriptionAsync(int id, CancellationToken cancellationToken = default);
}
