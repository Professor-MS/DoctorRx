using System.Collections.Generic;

namespace DoctorRx.Application.DTOs;

public record DashboardStatsDto(
    int TotalPatients,
    int PrescriptionsToday,
    int TotalPrescriptions,
    int TotalMedicinesInCatalog,
    IReadOnlyList<PrescriptionSummaryDto> RecentPrescriptions,
    IReadOnlyList<PatientDto> RecentPatients
);
