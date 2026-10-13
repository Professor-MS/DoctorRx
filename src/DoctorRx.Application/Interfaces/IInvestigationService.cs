using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IInvestigationService
{
    // Catalogue Management
    Task<IReadOnlyList<InvestigationCatalogItemDto>> GetCatalogItemsAsync(string? category = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationCatalogItemDto>> SearchCatalogAsync(string query, string? category = null, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<Result<InvestigationCatalogItemDto>> AddCustomInvestigationAsync(CreateInvestigationCatalogItemDto dto, CancellationToken cancellationToken = default);
    Task EnsureDefaultCatalogueSeededAsync(CancellationToken cancellationToken = default);

    // Request Orders
    Task<Result<InvestigationRequestDetailDto>> CreateInvestigationRequestAsync(CreateInvestigationRequestDto dto, CancellationToken cancellationToken = default);
    Task<InvestigationRequestDetailDto?> GetRequestByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<InvestigationRequestDetailDto?> GetRequestByNumberAsync(string requestNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequestSummaryDto>> GetRequestsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequestSummaryDto>> GetRecentRequestsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequestSummaryDto>> SearchRequestsAsync(string? query, DateOnly? fromDate, DateOnly? toDate, int? patientId, int maxResults = 100, CancellationToken cancellationToken = default);
}
