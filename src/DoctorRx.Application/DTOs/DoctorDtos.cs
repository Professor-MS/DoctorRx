using DoctorRx.Domain.Common;

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
    string? FooterText,
    string TitlePrefix = "Dr.",
    string RegistrationLabel = "Reg. No."
)
{
    public string DisplayName => DoctorDisplayNameFormatter.Format(TitlePrefix, Name);
    public string DisplayCredentials => $"{DisplayName} ({Qualification}) - {Specialization}";
}

public class CreateDoctorDto
{
    public string TitlePrefix { get; set; } = "Dr.";
    public string Name { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string RegistrationLabel { get; set; } = "Reg. No.";
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

public class UpdateDoctorDto
{
    public int Id { get; set; }
    public string TitlePrefix { get; set; } = "Dr.";
    public string Name { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string RegistrationLabel { get; set; } = "Reg. No.";
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
