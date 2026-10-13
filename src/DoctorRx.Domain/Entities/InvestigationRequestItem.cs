using System;
using DoctorRx.Domain.Common;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// An individual investigation order line belonging to an InvestigationRequest.
/// Supports both laboratory and imaging tests with optional anatomical site and views.
/// </summary>
public class InvestigationRequestItem : EntityBase
{
    public int RequestId { get; set; }
    public InvestigationRequest? Request { get; set; }

    public int? CatalogItemId { get; set; }
    public InvestigationCatalogItem? CatalogItem { get; set; }

    public string InvestigationName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }

    /// <summary>
    /// For imaging or targeted tests: anatomical site / body part (e.g. "Chest", "Right Knee", "Lumbar Spine").
    /// </summary>
    public string? BodyPartOrSite { get; set; }

    /// <summary>
    /// For imaging examinations: view or protocol requirements (e.g. "PA & Lateral", "Plain", "3T MRI").
    /// </summary>
    public string? ViewOrProtocol { get; set; }

    /// <summary>
    /// Specific instructions or technician notes for this particular test line.
    /// </summary>
    public string? SpecificInstructions { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
