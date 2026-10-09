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
