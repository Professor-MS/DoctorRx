using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

public class PrescriptionSearchToken : EntityBase
{
    public int PrescriptionId { get; set; }
    public string Token { get; set; } = string.Empty;
    public PrescriptionSearchTokenType TokenType { get; set; }

    // Navigation property
    public Prescription Prescription { get; set; } = null!;
}
