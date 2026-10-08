using System;
using DoctorRx.Domain.Entities;

namespace DoctorRx.Application.Common;

public static class EntitySearchExtensions
{
    /// <summary>
    /// Refreshes all search fields and tokens on a Patient entity.
    /// Must be called on create, update, quick-register, seeding and any import.
    /// </summary>
    public static void RefreshSearchFields(this Patient patient)
    {
        patient.NormalizedName = SearchNormalizer.Normalize(patient.Name);
        var phoneDigits = SearchNormalizer.NormalizePhoneDigits(patient.Phone);
        patient.PhoneDigits = string.IsNullOrEmpty(phoneDigits) ? null : phoneDigits;

        patient.SearchTokens.Clear();
        var tokens = SearchNormalizer.GeneratePatientTokens(patient);
        foreach (var t in tokens)
        {
            patient.SearchTokens.Add(t);
        }
    }

    /// <summary>
    /// Refreshes all search fields and tokens on a Medicine entity.
    /// Must be called on create, update, seeding and any import.
    /// </summary>
    public static void RefreshSearchFields(this Medicine medicine)
    {
        medicine.NormalizedName = SearchNormalizer.Normalize(medicine.Name);

        medicine.SearchTokens.Clear();
        var tokens = SearchNormalizer.GenerateMedicineTokens(medicine);
        foreach (var t in tokens)
        {
            medicine.SearchTokens.Add(t);
        }
    }
}
