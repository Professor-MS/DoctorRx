using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class MedicineSearchService : IMedicineSearchService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly ILogger<MedicineSearchService> _logger;

    public MedicineSearchService(IUnitOfWorkFactory uowFactory, ILogger<MedicineSearchService> logger)
    {
        _uowFactory = uowFactory;
        _logger = logger;
    }

    public Task<IReadOnlyList<MedicineDto>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        return SearchAsync(new MedicineSearchCriteria
        {
            Query = query,
            MaxResults = maxResults,
            IncludeInactive = false
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<MedicineDto>> SearchAsync(MedicineSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (criteria == null) return Array.Empty<MedicineDto>();

        try
        {
            await using var uow = _uowFactory.Create();
            var list = await uow.Medicines.SearchAsync(
                criteria.Query,
                criteria.DosageForm,
                criteria.IncludeInactive,
                criteria.MaxResults,
                cancellationToken);

            return list.Select(m => new MedicineDto(
                m.Id,
                m.Name,
                m.GenericName,
                m.Form,
                m.Strength,
                m.IsActive,
                m.UsageCount
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search medicines with query '{Query}'", criteria.Query);
            return Array.Empty<MedicineDto>();
        }
    }
}
