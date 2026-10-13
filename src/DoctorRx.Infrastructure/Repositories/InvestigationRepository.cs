using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorRx.Infrastructure.Repositories;

public class InvestigationRepository : Repository<InvestigationRequest>, IInvestigationRepository
{
    public InvestigationRepository(DoctorRxDbContext context) : base(context)
    {
    }

    #region Catalog Methods

    public async Task<IReadOnlyList<InvestigationCatalogItem>> GetCatalogItemsAsync(
        string? category = null, 
        bool includeInactive = false, 
        CancellationToken cancellationToken = default)
    {
        var query = Context.InvestigationCatalogItems.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Category == category);
        }

        return await query
            .OrderByDescending(i => i.UsageCount)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationCatalogItem>> SearchCatalogAsync(
        string query, 
        string? category = null, 
        int maxResults = 50, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetCatalogItemsAsync(category, includeInactive: false, cancellationToken);
        }

        var trimmed = query.Trim();
        var baseQuery = Context.InvestigationCatalogItems.AsNoTracking().Where(i => i.IsActive);

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            baseQuery = baseQuery.Where(i => i.Category == category);
        }

        var results = await baseQuery
            .Where(i => EF.Functions.Like(i.Name, $"%{trimmed}%") ||
                        (i.CommonAbbreviation != null && EF.Functions.Like(i.CommonAbbreviation, $"%{trimmed}%")) ||
                        (i.Subcategory != null && EF.Functions.Like(i.Subcategory, $"%{trimmed}%")))
            .OrderByDescending(i => i.CommonAbbreviation != null && i.CommonAbbreviation == trimmed ? 100 : 0)
            .ThenByDescending(i => i.Name.StartsWith(trimmed) ? 50 : 0)
            .ThenByDescending(i => i.UsageCount)
            .ThenBy(i => i.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<InvestigationCatalogItem?> GetCatalogItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.InvestigationCatalogItems.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<InvestigationCatalogItem?> FindCatalogItemByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await Context.InvestigationCatalogItems
            .FirstOrDefaultAsync(i => EF.Functions.Collate(i.Name, "NOCASE") == trimmed, cancellationToken);
    }

    public async Task<InvestigationCatalogItem> AddCatalogItemAsync(InvestigationCatalogItem item, CancellationToken cancellationToken = default)
    {
        item.NormalizedName = item.Name.Trim().ToUpperInvariant();
        await Context.InvestigationCatalogItems.AddAsync(item, cancellationToken);
        return item;
    }

    public Task UpdateCatalogItemAsync(InvestigationCatalogItem item, CancellationToken cancellationToken = default)
    {
        item.NormalizedName = item.Name.Trim().ToUpperInvariant();
        Context.InvestigationCatalogItems.Update(item);
        return Task.CompletedTask;
    }

    public async Task IncrementCatalogUsageAsync(int catalogItemId, CancellationToken cancellationToken = default)
    {
        var item = await Context.InvestigationCatalogItems.FindAsync(new object[] { catalogItemId }, cancellationToken);
        if (item != null)
        {
            item.UsageCount++;
        }
    }

    #endregion

    #region Request Methods

    public async Task<string> GenerateNextRequestNumberAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var key = $"INV-{targetDate:yyyyMMdd}";

        var seq = await Context.NumberSequences.FindAsync(new object[] { key }, cancellationToken);
        if (seq == null)
        {
            var maxExisting = await Context.InvestigationRequests
                .Where(r => r.RequestDate == targetDate)
                .CountAsync(cancellationToken);

            seq = new NumberSequence
            {
                SequenceKey = key,
                CurrentValue = maxExisting + 1,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await Context.NumberSequences.AddAsync(seq, cancellationToken);
        }
        else
        {
            seq.CurrentValue++;
            seq.UpdatedAtUtc = DateTime.UtcNow;
        }

        return $"{key}-{seq.CurrentValue:D4}";
    }

    public async Task<InvestigationRequest> AddRequestAsync(InvestigationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            request.RequestNumber = await GenerateNextRequestNumberAsync(request.RequestDate, cancellationToken);
        }

        await Context.InvestigationRequests.AddAsync(request, cancellationToken);
        return request;
    }

    public async Task<InvestigationRequest?> GetRequestByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Context.InvestigationRequests
            .Include(r => r.Patient)
            .Include(r => r.Items.OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<InvestigationRequest?> GetRequestByNumberWithDetailsAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        return await Context.InvestigationRequests
            .Include(r => r.Patient)
            .Include(r => r.Items.OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationRequest>> GetRequestsByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        return await Context.InvestigationRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Items.OrderBy(i => i.DisplayOrder))
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.RequestDate)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationRequest>> GetRecentRequestsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        return await Context.InvestigationRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Items.OrderBy(i => i.DisplayOrder))
            .OrderByDescending(r => r.RequestDate)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationRequest>> SearchRequestsAsync(
        string? query, 
        DateOnly? fromDate, 
        DateOnly? toDate, 
        int? patientId, 
        int maxResults = 100, 
        CancellationToken cancellationToken = default)
    {
        var dbQuery = Context.InvestigationRequests
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Items.OrderBy(i => i.DisplayOrder))
            .AsQueryable();

        if (patientId.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.PatientId == patientId.Value);
        }

        if (fromDate.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.RequestDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.RequestDate <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmed = query.Trim();
            dbQuery = dbQuery.Where(r => 
                EF.Functions.Like(r.RequestNumber, $"%{trimmed}%") ||
                (r.Patient != null && EF.Functions.Like(r.Patient.Name, $"%{trimmed}%")) ||
                (r.Patient != null && EF.Functions.Like(r.Patient.RecordNumber, $"%{trimmed}%")) ||
                r.Items.Any(i => EF.Functions.Like(i.InvestigationName, $"%{trimmed}%")));
        }

        return await dbQuery
            .OrderByDescending(r => r.RequestDate)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    #endregion
}
