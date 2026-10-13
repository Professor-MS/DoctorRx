using System;
using DoctorRx.Domain.Models;

namespace DoctorRx.Presentation.Services;

public interface IPrescriptionHistoryFilterSessionService
{
    string SearchText { get; set; }
    PrescriptionDatePreset DatePreset { get; set; }
    DateOnly? FromDate { get; set; }
    DateOnly? ToDate { get; set; }
    PrescriptionStatusSearchFilter StatusFilter { get; set; }
    bool IncludeReplaced { get; set; }
    PrescriptionSortOption SortOption { get; set; }
    int PageNumber { get; set; }
    int PageSize { get; set; }
    double ScrollPosition { get; set; }
    int? SelectedPrescriptionId { get; set; }

    bool HasActiveFilters { get; }
    void Reset();
}

public class PrescriptionHistoryFilterSessionService : IPrescriptionHistoryFilterSessionService
{
    public string SearchText { get; set; } = string.Empty;
    public PrescriptionDatePreset DatePreset { get; set; } = PrescriptionDatePreset.All;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public PrescriptionStatusSearchFilter StatusFilter { get; set; } = PrescriptionStatusSearchFilter.Default;
    public bool IncludeReplaced { get; set; } = false;
    public PrescriptionSortOption SortOption { get; set; } = PrescriptionSortOption.NewestFirst;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public double ScrollPosition { get; set; } = 0;
    public int? SelectedPrescriptionId { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        DatePreset != PrescriptionDatePreset.All ||
        FromDate.HasValue ||
        ToDate.HasValue ||
        StatusFilter != PrescriptionStatusSearchFilter.Default ||
        IncludeReplaced ||
        SortOption != PrescriptionSortOption.NewestFirst;

    public void Reset()
    {
        SearchText = string.Empty;
        DatePreset = PrescriptionDatePreset.All;
        FromDate = null;
        ToDate = null;
        StatusFilter = PrescriptionStatusSearchFilter.Default;
        IncludeReplaced = false;
        SortOption = PrescriptionSortOption.NewestFirst;
        PageNumber = 1;
        PageSize = 50;
        ScrollPosition = 0;
        SelectedPrescriptionId = null;
    }
}
