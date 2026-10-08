namespace DoctorRx.Application.DTOs;

public enum PatientStatusFilter
{
    Active = 0,
    Archived = 1,
    All = 2
}

public enum PatientSortOption
{
    NameAsc = 0,
    LastVisitDesc = 1,
    RecentlyAddedDesc = 2
}

public record PatientFilterCriteria(
    string? SearchQuery = null,
    PatientStatusFilter StatusFilter = PatientStatusFilter.Active,
    PatientSortOption SortOption = PatientSortOption.NameAsc,
    int PageNumber = 1,
    int PageSize = 50
);
