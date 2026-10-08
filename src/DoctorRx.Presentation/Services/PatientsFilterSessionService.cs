using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Services;

public interface IPatientsFilterSessionService
{
    string SearchQuery { get; set; }
    PatientStatusFilter StatusFilter { get; set; }
    PatientSortOption SortOption { get; set; }
    int CurrentPage { get; set; }
    bool HasActiveFilters { get; }
    void Reset();
}

public class PatientsFilterSessionService : IPatientsFilterSessionService
{
    public string SearchQuery { get; set; } = string.Empty;
    public PatientStatusFilter StatusFilter { get; set; } = PatientStatusFilter.Active;
    public PatientSortOption SortOption { get; set; } = PatientSortOption.NameAsc;
    public int CurrentPage { get; set; } = 1;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchQuery) ||
        StatusFilter != PatientStatusFilter.Active ||
        SortOption != PatientSortOption.NameAsc;

    public void Reset()
    {
        SearchQuery = string.Empty;
        StatusFilter = PatientStatusFilter.Active;
        SortOption = PatientSortOption.NameAsc;
        CurrentPage = 1;
    }
}
