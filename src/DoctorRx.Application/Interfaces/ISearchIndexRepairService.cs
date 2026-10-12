using System.Threading;
using System.Threading.Tasks;

namespace DoctorRx.Application.Interfaces;

public record SearchIndexRepairReport(int PatientsChecked, int PatientsRepaired, int MedicinesChecked, int MedicinesRepaired, int PrescriptionsChecked = 0, int PrescriptionsRepaired = 0);

public interface ISearchIndexRepairService
{
    Task<SearchIndexRepairReport> RepairIndexAsync(CancellationToken cancellationToken = default);
}
