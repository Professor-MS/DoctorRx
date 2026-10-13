using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Application.Services;

public class MedicineCsvImportService : IMedicineCsvImportService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly ILogger<MedicineCsvImportService> _logger;

    private class DefaultClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    }

    public MedicineCsvImportService(
        IUnitOfWorkFactory uowFactory,
        IClock? clock = null,
        ILogger<MedicineCsvImportService>? logger = null)
    {
        _uowFactory = uowFactory;
        _clock = clock ?? new DefaultClock();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MedicineCsvImportService>.Instance;
    }

    public async Task<MedicineImportResult> ImportFromCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            var msg = $"CSV file not found at '{filePath}'.";
            _logger.LogWarning(msg);
            return new MedicineImportResult(0, 0, 0, 1, new List<string> { msg });
        }

        await using var stream = File.OpenRead(filePath);
        return await ImportFromStreamAsync(stream, cancellationToken);
    }

    public async Task<MedicineImportResult> ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rawContent = await reader.ReadToEndAsync(cancellationToken);

        var rows = ParseCsv(rawContent);
        if (rows.Count == 0)
        {
            return new MedicineImportResult(0, 0, 0, 0, new List<string> { "CSV file is empty." });
        }

        // Header row mapping
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int idxMedicineId = header.IndexOf("medicine_id");
        int idxBrandName = header.IndexOf("brand_name");
        int idxGenericName = header.IndexOf("generic_name");
        int idxGenericFormula = header.IndexOf("generic_formula");
        int idxStrength = header.IndexOf("strength");
        int idxDosageForm = header.IndexOf("dosage_form");
        int idxRoute = header.IndexOf("route_of_administration");
        int idxCategory = header.IndexOf("therapeutic_category");
        int idxIndications = header.IndexOf("common_indications");
        int idxNotes = header.IndexOf("notes");

        if (idxGenericName < 0 && idxBrandName < 0)
        {
            return new MedicineImportResult(0, 0, 0, 1, new List<string> { "CSV header is missing required 'generic_name' or 'brand_name' column." });
        }

        int totalProcessed = 0;
        int importedCount = 0;
        int skippedCount = 0;
        int rejectedCount = 0;
        var errorMessages = new List<string>();

        // Track items added in the current batch for in-file deduplication
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var uow = _uowFactory.Create();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
            {
                continue; // Skip blank lines
            }

            totalProcessed++;
            int rowNumber = i + 1;

            string GetCol(int colIdx) => (colIdx >= 0 && colIdx < row.Count) ? row[colIdx].Trim() : string.Empty;

            var rawBrand = GetCol(idxBrandName);
            var rawGeneric = GetCol(idxGenericName);
            var rawFormula = GetCol(idxGenericFormula);
            var rawStrength = GetCol(idxStrength);
            var rawDosageForm = GetCol(idxDosageForm);

            // Determine primary name
            string name = !string.IsNullOrWhiteSpace(rawBrand)
                ? rawBrand
                : (!string.IsNullOrWhiteSpace(rawGeneric) ? rawGeneric : rawFormula);

            if (string.IsNullOrWhiteSpace(name))
            {
                rejectedCount++;
                errorMessages.Add($"Row {rowNumber}: Rejected because medicine name and generic name are both empty.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawDosageForm))
            {
                rejectedCount++;
                errorMessages.Add($"Row {rowNumber} ('{name}'): Rejected because dosage form is required.");
                continue;
            }

            string? genericName = !string.IsNullOrWhiteSpace(rawGeneric)
                ? rawGeneric
                : (!string.IsNullOrWhiteSpace(rawFormula) ? rawFormula : null);

            var form = rawDosageForm;
            var strength = rawStrength;

            // In-batch deduplication key: name|form|normalizedStrength
            var batchKey = $"{SearchNormalizer.Normalize(name)}|{form.ToLowerInvariant()}|{NormalizeStrength(strength)}";
            if (seenInBatch.Contains(batchKey))
            {
                skippedCount++;
                continue;
            }

            // Database duplicate check
            var existingDuplicates = await uow.Medicines.FindPotentialDuplicatesAsync(
                name,
                form,
                strength,
                cancellationToken: cancellationToken);

            var rawCategory = GetCol(idxCategory);
            var rawIndications = GetCol(idxIndications);

            string? description = !string.IsNullOrWhiteSpace(rawIndications)
                ? rawIndications
                : (!string.IsNullOrWhiteSpace(rawCategory)
                    ? rawCategory
                    : DoctorRx.Application.Common.MedicineClinicalKnowledge.GetClinicalIndication(name, genericName));

            if (existingDuplicates.Count > 0)
            {
                var existing = existingDuplicates[0];
                if (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(description))
                {
                    existing.Description = description;
                    await uow.Medicines.UpdateAsync(existing, cancellationToken);
                }

                skippedCount++;
                seenInBatch.Add(batchKey);
                continue;
            }

            // Valid new medicine formulation
            var medicine = new Medicine
            {
                Name = name,
                GenericName = genericName,
                Form = form,
                Strength = strength,
                Description = description,
                IsActive = true,
                UsageCount = 0,
                CreatedAtUtc = _clock.UtcNow
            };
            medicine.RefreshSearchFields();

            await uow.Medicines.AddAsync(medicine, cancellationToken);
            seenInBatch.Add(batchKey);
            importedCount++;
        }

        if (importedCount > 0)
        {
            await uow.CommitAsync(cancellationToken);
            _logger.LogInformation("Successfully imported {ImportedCount} medicines from CSV ({SkippedCount} skipped, {RejectedCount} rejected).",
                importedCount, skippedCount, rejectedCount);
        }

        return new MedicineImportResult(totalProcessed, importedCount, skippedCount, rejectedCount, errorMessages);
    }

    /// <summary>
    /// Robust RFC-4180 compliant CSV parser that handles multi-line fields, escaped quotes (""), and commas inside quotes.
    /// </summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return records;

        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        // Skip UTF-8 BOM if present
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            i = 1;
        }

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                        continue;
                    }
                }
                else
                {
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                else if (c == ',')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                    continue;
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                    i++;
                    continue;
                }
                else if (c == '\n')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                    i++;
                    continue;
                }
                else
                {
                    currentField.Append(c);
                    i++;
                    continue;
                }
            }
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add(currentRecord);
        }

        return records;
    }

    private static string NormalizeStrength(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;
        return Regex.Replace(str.Trim(), @"\s+", "").ToLowerInvariant();
    }
}
