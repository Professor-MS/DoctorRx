using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.ValueObjects;

public class PatientSnapshot
{
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.NotSpecified;
    public string AgeText { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? KnownAllergies { get; set; }

    public PatientSnapshot() { }

    public PatientSnapshot(
        string name,
        Gender gender,
        string ageText,
        string? phone,
        string? knownAllergies)
    {
        Name = name;
        Gender = gender;
        AgeText = ageText;
        Phone = phone;
        KnownAllergies = knownAllergies;
    }
}
