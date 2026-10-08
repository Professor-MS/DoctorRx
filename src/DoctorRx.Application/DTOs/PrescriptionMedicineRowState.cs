using System;
using DoctorRx.Domain.Enums;

namespace DoctorRx.Application.DTOs;

public class PrescriptionMedicineRowState
{
    public Guid RowId { get; set; } = Guid.NewGuid();
    public int? MedicineId { get; set; }

    // Formulation identity (non-clinical pre-filling from catalog allowed, always editable)
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;

    // Clinical directions (Zero defaults - physician must explicitly decide each)
    public string Dose { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? Timing { get; set; }
    public MealRelation MealRelation { get; set; } = MealRelation.AsDirected;
    public string? CustomMealRelationText { get; set; }
    public string? WithWhat { get; set; }
    public string Duration { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? Instructions { get; set; }

    // Catalog persistence choice for custom/unregistered medicines
    public bool AddToCatalogIfCustom { get; set; } = true;

    public PrescriptionMedicineRowState Clone()
    {
        return new PrescriptionMedicineRowState
        {
            RowId = RowId,
            MedicineId = MedicineId,
            MedicineName = MedicineName,
            GenericName = GenericName,
            Form = Form,
            Strength = Strength,
            Dose = Dose,
            Frequency = Frequency,
            Timing = Timing,
            MealRelation = MealRelation,
            CustomMealRelationText = CustomMealRelationText,
            WithWhat = WithWhat,
            Duration = Duration,
            Route = Route,
            Instructions = Instructions,
            AddToCatalogIfCustom = AddToCatalogIfCustom
        };
    }
}
