using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

public class PatientSearchToken : EntityBase
{
    public int PatientId { get; set; }
    public string Token { get; set; } = string.Empty;
    public SearchTokenType TokenType { get; set; }

    // Navigation property
    public Patient Patient { get; set; } = null!;
}
