namespace DoctorRx.Application.DTOs;

public record MedicineDto(
    int Id,
    string Name,
    string? GenericName,
    string Form,
    string Strength,
    string? DefaultDose,
    string? DefaultFrequency,
    string? DefaultRoute,
    string? DefaultInstructions,
    bool IsActive
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
    public string Form { get; set; } = "Tablet";
    public string Strength { get; set; } = string.Empty;
    public string? DefaultDose { get; set; }
    public string? DefaultFrequency { get; set; }
    public string? DefaultRoute { get; set; } = "Oral";
    public string? DefaultInstructions { get; set; }
}

public class UpdateMedicineDto : CreateMedicineDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
}
