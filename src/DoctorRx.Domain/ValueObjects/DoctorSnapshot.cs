namespace DoctorRx.Domain.ValueObjects;

public class DoctorSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicAddress { get; set; }
    public string? ClinicPhone { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }

    public DoctorSnapshot() { }

    public DoctorSnapshot(
        string name,
        string qualification,
        string registrationNumber,
        string specialization,
        string? phone,
        string clinicName,
        string? clinicAddress,
        string? clinicPhone,
        string? headerText,
        string? footerText)
    {
        Name = name;
        Qualification = qualification;
        RegistrationNumber = registrationNumber;
        Specialization = specialization;
        Phone = phone;
        ClinicName = clinicName;
        ClinicAddress = clinicAddress;
        ClinicPhone = clinicPhone;
        HeaderText = headerText;
        FooterText = footerText;
    }
}
