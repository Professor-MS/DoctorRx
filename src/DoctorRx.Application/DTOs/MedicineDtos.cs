namespace DoctorRx.Application.DTOs;

public record MedicineDto(
    int Id,
    string Name,
    string? GenericName,
    string Form,
    string Strength,
    bool IsActive,
    int UsageCount = 0,
    string? Description = null
)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Strength)
        ? $"{Name} ({Form})"
        : $"{Name} {Strength} ({Form})";

    public string BrandDisplay => string.IsNullOrWhiteSpace(GenericName) || Name.Equals(GenericName, System.StringComparison.OrdinalIgnoreCase)
        ? "Generic"
        : Name;

    public string GenericFormulaDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GenericName) && !GenericName.Equals(Name, System.StringComparison.OrdinalIgnoreCase))
            {
                return $"Generic: {GenericName}";
            }

            // When Brand name is the generic name (e.g. Acyclovir, Paracetamol, Metformin),
            // show the drug class/category instead of redundantly repeating the same name!
            var category = DoctorRx.Application.Common.MedicineClinicalKnowledge.GetTherapeuticCategory(Name, GenericName);
            if (!string.IsNullOrWhiteSpace(category))
            {
                return category;
            }

            return "Formulary active substance";
        }
    }

    public string DescriptionDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Description))
            {
                return Description;
            }

            var indication = DoctorRx.Application.Common.MedicineClinicalKnowledge.GetClinicalIndication(Name, GenericName);
            if (!string.IsNullOrWhiteSpace(indication))
            {
                return indication;
            }

            return "Clinical indication as directed by physician";
        }
    }

    public bool HasLongDescription => (DescriptionDisplay?.Length ?? 0) > 60;

    public string FormBadgeBackground
    {
        get
        {
            var f = (Form ?? string.Empty).ToLowerInvariant();
            if (f.Contains("tablet")) return "#EFF6FF"; // Soft blue
            if (f.Contains("capsule")) return "#F5F3FF"; // Soft purple
            if (f.Contains("injection") || f.Contains("infusion")) return "#FEF2F2"; // Soft rose/red
            if (f.Contains("syrup") || f.Contains("suspension") || f.Contains("solution")) return "#ECFDF5"; // Soft emerald
            if (f.Contains("drop")) return "#ECFEFF"; // Soft cyan
            if (f.Contains("cream") || f.Contains("ointment") || f.Contains("gel") || f.Contains("lotion")) return "#FFFBEB"; // Soft amber
            if (f.Contains("inhaler")) return "#FAF5FF"; // Soft fuchsia
            return "#F1F5F9"; // Soft slate
        }
    }

    public string FormBadgeForeground
    {
        get
        {
            var f = (Form ?? string.Empty).ToLowerInvariant();
            if (f.Contains("tablet")) return "#1D4ED8"; // Blue 700
            if (f.Contains("capsule")) return "#6D28D9"; // Purple 700
            if (f.Contains("injection") || f.Contains("infusion")) return "#B91C1C"; // Red 700
            if (f.Contains("syrup") || f.Contains("suspension") || f.Contains("solution")) return "#047857"; // Emerald 700
            if (f.Contains("drop")) return "#0E7490"; // Cyan 700
            if (f.Contains("cream") || f.Contains("ointment") || f.Contains("gel") || f.Contains("lotion")) return "#B45309"; // Amber 700
            if (f.Contains("inhaler")) return "#A21CAF"; // Fuchsia 700
            return "#475569"; // Slate 700
        }
    }

    public string FormBadgeBorder
    {
        get
        {
            var f = (Form ?? string.Empty).ToLowerInvariant();
            if (f.Contains("tablet")) return "#BFDBFE";
            if (f.Contains("capsule")) return "#DDD6FE";
            if (f.Contains("injection") || f.Contains("infusion")) return "#FECACA";
            if (f.Contains("syrup") || f.Contains("suspension") || f.Contains("solution")) return "#A7F3D0";
            if (f.Contains("drop")) return "#A5F3FC";
            if (f.Contains("cream") || f.Contains("ointment") || f.Contains("gel") || f.Contains("lotion")) return "#FDE68A";
            if (f.Contains("inhaler")) return "#F5D0FE";
            return "#CBD5E1";
        }
    }
}

public record MedicineImportResult(
    int TotalProcessed,
    int ImportedCount,
    int SkippedCount,
    int RejectedCount,
    System.Collections.Generic.IReadOnlyList<string> ErrorMessages
);

public class CreateMedicineDto
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateMedicineDto : CreateMedicineDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
}

public record MedicineUsageSummaryDto(
    int MedicineId,
    string MedicineName,
    bool IsReferencedInPrescriptions,
    int PrescriptionReferenceCount,
    bool CanHardDelete,
    int DraftReferenceCount = 0
)
{
    public bool IsReferencedInDrafts => DraftReferenceCount > 0;
};

public class MedicineSearchCriteria
{
    public string Query { get; set; } = string.Empty;
    public string? DosageForm { get; set; }
    public bool IncludeInactive { get; set; } = false;
    public int MaxResults { get; set; } = 50;
}
