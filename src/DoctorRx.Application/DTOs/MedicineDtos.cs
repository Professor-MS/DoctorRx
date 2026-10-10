namespace DoctorRx.Application.DTOs;

public record MedicineDto(
    int Id,
    string Name,
    string? GenericName,
    string Form,
    string Strength,
    bool IsActive,
    int UsageCount = 0
)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Strength)
        ? $"{Name} ({Form})"
        : $"{Name} {Strength} ({Form})";
}

public class CreateMedicineDto
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
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
