namespace DoctorRx.Application.DTOs;

public record DoctorDto(
    int Id,
    string Name,
    string Qualification,
    string RegistrationNumber,
    string Specialization,
    string? Phone,
    string? Email,
    string ClinicName,
    string? ClinicAddress,
    string? ClinicPhone,
    string? HeaderText,
    string? FooterText
)
{
    public string DisplayCredentials => $"{Name} ({Qualification}) - {Specialization}";
}

public class UpdateDoctorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicAddress { get; set; }
    public string? ClinicPhone { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
}
