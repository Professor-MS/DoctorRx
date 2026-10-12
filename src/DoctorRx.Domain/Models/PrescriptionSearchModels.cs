using System;
using System.Collections.Generic;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Models;

public enum PrescriptionDatePreset
{
    All = 0,
    Today = 1,
    Last7Days = 2,
    ThisMonth = 3,
    Custom = 4
}

public enum PrescriptionStatusSearchFilter
{
    Default = 0,    // Issued + Cancelled (Replaced hidden unless IncludeReplaced = true)
    Issued = 1,
    Cancelled = 2,
    Replaced = 3,
    All = 4
}

public enum PrescriptionSortOption
{
    NewestFirst = 0,
    OldestFirst = 1,
    PatientName = 2,
    PrescriptionNumber = 3
}

public record PrescriptionSearchCriteria(
    string? SearchText = null,
    PrescriptionDatePreset DatePreset = PrescriptionDatePreset.All,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    PrescriptionStatusSearchFilter StatusFilter = PrescriptionStatusSearchFilter.Default,
    bool IncludeReplaced = false,
    int? PatientId = null,
    PrescriptionSortOption SortOption = PrescriptionSortOption.NewestFirst,
    int PageNumber = 1,
    int PageSize = 50
);

public record PrescriptionHistoryItemDto(
    int Id,
    string PrescriptionNumber,
    int AmendmentNumber,
    DateOnly PrescriptionDate,
    string PatientName,
    string PatientRecordNumber,
    int MedicineCount,
    PrescriptionStatus Status,
    string? ReplacedByNumber,
    DateTime FinalizedAtUtc
);

public record PrescriptionHistoryPagedResult(
    IReadOnlyList<PrescriptionHistoryItemDto> Items,
    int TotalCount,
    bool IsCountCapped,
    int PageNumber,
    int PageSize
)
{
    public bool HasMore => (PageNumber * PageSize) < TotalCount;
    public string FormattedTotalCount => IsCountCapped ? $"{TotalCount:N0}+" : $"{TotalCount:N0}";
}
