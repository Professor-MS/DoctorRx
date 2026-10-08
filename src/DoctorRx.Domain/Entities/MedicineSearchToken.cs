using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

public class MedicineSearchToken : EntityBase
{
    public int MedicineId { get; set; }
    public string Token { get; set; } = string.Empty;
    public SearchTokenType TokenType { get; set; }

    // Navigation property
    public Medicine Medicine { get; set; } = null!;
}
