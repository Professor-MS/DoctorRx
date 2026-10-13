using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineCsvImportTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestDbContextFactory _factory;
    private readonly UnitOfWorkFactory _uowFactory;

    public MedicineCsvImportTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_CsvTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        var dbPath = Path.Combine(_testDir, "test.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .Options;

        _factory = new TestDbContextFactory(options);
        using (var initCtx = _factory.CreateDbContext())
        {
            initCtx.Database.Migrate();
        }

        _uowFactory = new UnitOfWorkFactory(_factory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task ImportFromCsvAsync_RealPakistanMedicineMasterCsv_ImportsSuccessfully()
    {
        // Arrange
        var importService = new MedicineCsvImportService(_uowFactory);

        // Find actual CSV file
        var csvPath = Path.Combine(AppContext.BaseDirectory, "pakistan_medicine_master.csv");
        if (!File.Exists(csvPath))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(csvPath))
            {
                var candidate = Path.Combine(dir.FullName, "pakistan_medicine_master.csv");
                if (File.Exists(candidate))
                {
                    csvPath = candidate;
                    break;
                }
                dir = dir.Parent;
            }
        }

        Assert.True(File.Exists(csvPath), $"Could not find pakistan_medicine_master.csv at {csvPath}");

        // Act - First Import
        var result = await importService.ImportFromCsvAsync(csvPath);

        // Assert
        Assert.Equal(90, result.TotalProcessed);
        Assert.Equal(90, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.RejectedCount);
        Assert.Empty(result.ErrorMessages);

        using (var ctx = _factory.CreateDbContext())
        {
            var countInDb = await ctx.Medicines.CountAsync();
            Assert.Equal(90, countInDb);

            // Verify tokens were generated
            var tokenCount = await ctx.MedicineSearchTokens.CountAsync();
            Assert.True(tokenCount > 0, "Medicine search tokens should be populated");
        }

        // Act - Reimport same CSV (Duplicate Check)
        var reimportResult = await importService.ImportFromCsvAsync(csvPath);

        // Assert - All 90 should be detected as duplicates and skipped without inserting
        Assert.Equal(90, reimportResult.TotalProcessed);
        Assert.Equal(0, reimportResult.ImportedCount);
        Assert.Equal(90, reimportResult.SkippedCount);
        Assert.Equal(0, reimportResult.RejectedCount);

        using (var ctx = _factory.CreateDbContext())
        {
            var countAfterReimport = await ctx.Medicines.CountAsync();
            Assert.Equal(90, countAfterReimport);
        }
    }

    [Fact]
    public async Task ImportFromCsvAsync_InvalidAndValidRecords_HandlesGracefully()
    {
        // Arrange
        var importService = new MedicineCsvImportService(_uowFactory);

        var tempCsv = Path.GetTempFileName();
        try
        {
            var csvContent =
                "medicine_id,brand_name,generic_name,generic_formula,strength,dosage_form\n" +
                "PKM001,Panadol,Paracetamol,Paracetamol,500 mg,Tablet\n" + // Valid brand
                "PKM002,,Amoxicillin,Amoxicillin,250 mg,Capsule\n" +        // Valid generic-as-name
                ",,,,,\n" +                                                 // Empty row -> rejected
                "PKM004,,,,500 mg,Tablet\n";                               // Missing both brand & generic -> rejected

            await File.WriteAllTextAsync(tempCsv, csvContent);

            // Act
            var result = await importService.ImportFromCsvAsync(tempCsv);

            // Assert
            Assert.Equal(4, result.TotalProcessed);
            Assert.Equal(2, result.ImportedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(2, result.RejectedCount);
            Assert.Equal(2, result.ErrorMessages.Count);

            using (var ctx = _factory.CreateDbContext())
            {
                var inDb = await ctx.Medicines.ToListAsync();
                Assert.Equal(2, inDb.Count);
                Assert.Contains(inDb, m => m.Name == "Panadol" && m.GenericName == "Paracetamol");
                Assert.Contains(inDb, m => m.Name == "Amoxicillin");
            }
        }
        finally
        {
            if (File.Exists(tempCsv))
                File.Delete(tempCsv);
        }
    }

    [Fact]
    public async Task ImportFromCsvAsync_QuotedFieldsWithCommasAndQuotes_ParsesCorrectly()
    {
        // Arrange
        var importService = new MedicineCsvImportService(_uowFactory);

        var tempCsv = Path.GetTempFileName();
        try
        {
            var csvContent =
                "medicine_id,brand_name,generic_name,generic_formula,strength,dosage_form,notes\n" +
                "PKM040,,\"Amoxicillin + clavulanic acid\",\"Amoxicillin + clavulanic acid\",\"500 mg + 125 mg\",Tablet,\"Sample note with \"\"quotes\"\" and commas, here.\"\n";

            await File.WriteAllTextAsync(tempCsv, csvContent);

            // Act
            var result = await importService.ImportFromCsvAsync(tempCsv);

            // Assert
            Assert.Equal(1, result.TotalProcessed);
            Assert.Equal(1, result.ImportedCount);

            using (var ctx = _factory.CreateDbContext())
            {
                var med = await ctx.Medicines.FirstAsync();
                Assert.Equal("Amoxicillin + clavulanic acid", med.Name);
                Assert.Equal("500 mg + 125 mg", med.Strength);
                Assert.Equal("Tablet", med.Form);
            }
        }
        finally
        {
            if (File.Exists(tempCsv))
                File.Delete(tempCsv);
        }
    }
}
