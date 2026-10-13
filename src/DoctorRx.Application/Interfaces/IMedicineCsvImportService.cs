using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Application.Interfaces;

public interface IMedicineCsvImportService
{
    Task<MedicineImportResult> ImportFromCsvAsync(string filePath, CancellationToken cancellationToken = default);
    Task<MedicineImportResult> ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken = default);
}
