using System;
using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Diagnostic test or examination entry in the clinic's investigation catalogue.
/// Backs laboratory tests, radiology & imaging examinations, and special diagnostic procedures.
/// </summary>
public class InvestigationCatalogItem : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Top-level category: "Radiology & Imaging", "Laboratory", "Cardiology & Diagnostic Procedures", etc.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific modality or lab discipline: "X-Ray", "MRI", "CT Scan", "Ultrasound", "Hematology", "Biochemistry", "Microbiology", etc.
    /// </summary>
    public string? Subcategory { get; set; }

    /// <summary>
    /// Standard medical acronym / abbreviation (e.g., "CBC", "LFT", "RFT", "ECG", "ECHO", "USG", "CT", "MRI").
    /// </summary>
    public string? CommonAbbreviation { get; set; }

    /// <summary>
    /// Sample type or examination modality (e.g. "Whole Blood (EDTA)", "Serum", "Midstream Urine", "Plain / Contrast Imaging").
    /// </summary>
    public string? SampleTypeOrModality { get; set; }

    /// <summary>
    /// Default preparation guidelines or instructions for patient orientation (optional notes).
    /// </summary>
    public string? DefaultInstructions { get; set; }

    /// <summary>
    /// Indicates whether this test was added as a custom clinic entry by the doctor.
    /// </summary>
    public bool IsCustom { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tracks clinical ordering frequency for ordering recommendations.
    /// </summary>
    public int UsageCount { get; set; }
}
