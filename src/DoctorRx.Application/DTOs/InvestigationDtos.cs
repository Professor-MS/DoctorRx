using System;
using System.Collections.Generic;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.DTOs;

public record InvestigationCatalogItemDto(
    int Id,
    string Name,
    string Category,
    string? Subcategory,
    string? CommonAbbreviation,
    string? SampleTypeOrModality,
    string? DefaultInstructions,
    bool IsCustom,
    bool IsActive,
    int UsageCount
)
{
    public string DisplayName => !string.IsNullOrWhiteSpace(CommonAbbreviation)
        ? $"{Name} ({CommonAbbreviation})"
        : Name;

    public string CategoryBadgeBackground => Category switch
    {
        "Radiology & Imaging" or "Imaging" or "Radiology" => "#EFF6FF", // Blue 50
        "Laboratory" or "Lab" => "#ECFDF5", // Emerald 50
        "Cardiology & Diagnostics" or "Cardiology" => "#FFF1F2", // Rose 50
        _ => "#F1F5F9" // Slate 100
    };

    public string CategoryBadgeForeground => Category switch
    {
        "Radiology & Imaging" or "Imaging" or "Radiology" => "#1D4ED8", // Blue 700
        "Laboratory" or "Lab" => "#047857", // Emerald 700
        "Cardiology & Diagnostics" or "Cardiology" => "#BE123C", // Rose 700
        _ => "#475569" // Slate 700
    };

    public string CategoryBadgeBorder => Category switch
    {
        "Radiology & Imaging" or "Imaging" or "Radiology" => "#BFDBFE",
        "Laboratory" or "Lab" => "#A7F3D0",
        "Cardiology & Diagnostics" or "Cardiology" => "#FECDD3",
        _ => "#CBD5E1"
    };

    public bool HasAbbreviation => !string.IsNullOrWhiteSpace(CommonAbbreviation);
    public string Abbreviation => CommonAbbreviation ?? string.Empty;
}

public class CreateInvestigationCatalogItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Laboratory";
    public string? Subcategory { get; set; }
    public string? CommonAbbreviation { get; set; }
    public string? SampleTypeOrModality { get; set; }
    public string? DefaultInstructions { get; set; }
}

public class CreateInvestigationRequestItemDto
{
    public int? CatalogItemId { get; set; }
    public string InvestigationName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string? BodyPartOrSite { get; set; }
    public string? ViewOrProtocol { get; set; }
    public string? SpecificInstructions { get; set; }
    public int DisplayOrder { get; set; }
}

public record InvestigationRequestItemDto(
    int Id,
    int RequestId,
    int? CatalogItemId,
    string InvestigationName,
    string Category,
    string? Subcategory,
    string? BodyPartOrSite,
    string? ViewOrProtocol,
    string? SpecificInstructions,
    int DisplayOrder
)
{
    public string FormattedDetails
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(BodyPartOrSite)) parts.Add($"Site: {BodyPartOrSite.Trim()}");
            if (!string.IsNullOrWhiteSpace(ViewOrProtocol)) parts.Add($"Protocol: {ViewOrProtocol.Trim()}");
            if (!string.IsNullOrWhiteSpace(SpecificInstructions)) parts.Add($"Instructions: {SpecificInstructions.Trim()}");
            return parts.Count > 0 ? string.Join(" | ", parts) : string.Empty;
        }
    }
    public bool HasFormattedDetails => !string.IsNullOrWhiteSpace(FormattedDetails);
    public bool HasSpecificInstructions => !string.IsNullOrWhiteSpace(SpecificInstructions);
}

public class CreateInvestigationRequestDto
{
    public int PatientId { get; set; }
    public DateOnly RequestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? ClinicalNotes { get; set; }
    public string? Instructions { get; set; }
    public List<CreateInvestigationRequestItemDto> Items { get; set; } = new();
}

public record InvestigationRequestSummaryDto(
    int Id,
    string RequestNumber,
    int PatientId,
    string PatientName,
    string PatientRecordNumber,
    string PatientAge,
    Gender PatientGender,
    DateOnly RequestDate,
    DateTime CreatedAtUtc,
    string Status,
    int ItemCount,
    string ItemSummary
)
{
    public string FormattedDate => RequestDate.ToString("dd MMM yyyy");
    public string SummaryText => ItemSummary;
    public string PatientMrn => PatientRecordNumber;
}

public record InvestigationRequestDetailDto(
    int Id,
    string RequestNumber,
    int PatientId,
    string PatientName,
    string PatientRecordNumber,
    string PatientAge,
    Gender PatientGender,
    string? PatientPhone,
    string? PatientAddress,
    string? PatientKnownAllergies,
    DateOnly RequestDate,
    DateTime CreatedAtUtc,
    string Status,
    string? ClinicalNotes,
    string? Instructions,
    string? DoctorName,
    string? DoctorQualification,
    string? DoctorRegistration,
    string? DoctorSpecialization,
    string? ClinicName,
    string? ClinicAddress,
    string? ClinicPhone,
    IReadOnlyList<InvestigationRequestItemDto> Items
)
{
    public string FormattedDate => RequestDate.ToString("dd MMM yyyy");
    public string PatientMrn => PatientRecordNumber;
}
