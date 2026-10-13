using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Domain.Interfaces;

public interface IInvestigationRepository : IRepository<InvestigationRequest>
{
    // Catalog methods
    Task<IReadOnlyList<InvestigationCatalogItem>> GetCatalogItemsAsync(string? category = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationCatalogItem>> SearchCatalogAsync(string query, string? category = null, int maxResults = 50, CancellationToken cancellationToken = default);
    Task<InvestigationCatalogItem?> GetCatalogItemByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<InvestigationCatalogItem?> FindCatalogItemByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<InvestigationCatalogItem> AddCatalogItemAsync(InvestigationCatalogItem item, CancellationToken cancellationToken = default);
    Task UpdateCatalogItemAsync(InvestigationCatalogItem item, CancellationToken cancellationToken = default);
    Task IncrementCatalogUsageAsync(int catalogItemId, CancellationToken cancellationToken = default);

    // Request methods
    Task<string> GenerateNextRequestNumberAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<InvestigationRequest> AddRequestAsync(InvestigationRequest request, CancellationToken cancellationToken = default);
    Task<InvestigationRequest?> GetRequestByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<InvestigationRequest?> GetRequestByNumberWithDetailsAsync(string requestNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequest>> GetRequestsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequest>> GetRecentRequestsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationRequest>> SearchRequestsAsync(string? query, DateOnly? fromDate, DateOnly? toDate, int? patientId, int maxResults = 100, CancellationToken cancellationToken = default);
}
