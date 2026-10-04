using DoctorRx.Domain.Common;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Domain.Entities;

/// <summary>
/// Represents an individual medicine prescribed in a prescription.
/// IMPORTANT: All medicine attributes are stored as immutable snapshots at the time of prescription.
/// Subsequent changes to the master medicine catalog will NEVER alter historical prescription records.
/// </summary>
public class PrescriptionMedicine : EntityBase
{
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    /// <summary>
    /// Optional foreign key to master medicine catalogue.
    /// Can be null if the doctor entered an uncatalogued or custom medicine.
    /// </summary>
    public int? MedicineId { get; set; }

    // Snapshot fields preserved for medical and legal accuracy:
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = "Tablet";
    public string Strength { get; set; } = string.Empty;

    // Prescription directions:
    public string Dose { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? Timing { get; set; }
    public MealRelation MealRelation { get; set; } = MealRelation.AsDirected;
    public string? CustomMealRelationText { get; set; }
    public string Route { get; set; } = "Oral";
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Creates an immutable prescription item from a master medicine entity.
    /// </summary>
    public static PrescriptionMedicine FromMedicine(Medicine medicine, int sortOrder = 0)
    {
        return new PrescriptionMedicine
        {
            MedicineId = medicine.Id,
            MedicineName = medicine.Name,
            GenericName = medicine.GenericName,
            Form = medicine.Form,
            Strength = medicine.Strength,
            Dose = medicine.DefaultDose ?? "1",
            Frequency = medicine.DefaultFrequency ?? "1-0-1",
            Route = medicine.DefaultRoute ?? "Oral",
            Instructions = medicine.DefaultInstructions,
            SortOrder = sortOrder
        };
    }
}
