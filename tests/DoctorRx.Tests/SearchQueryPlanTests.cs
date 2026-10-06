using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class SearchQueryPlanTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public SearchQueryPlanTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_PlanTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        _factory = new TestDbContextFactory(options);

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        migrator.MigrateDatabaseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }

    private async Task<List<string>> ExplainQueryPlanAsync(string sql)
    {
        var planDetails = new List<string>();
        await using var context = await _factory.CreateDbContextAsync();
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // SQLite EXPLAIN QUERY PLAN column 3 is 'detail'
            var detail = reader.GetString(reader.GetOrdinal("detail"));
            planDetails.Add(detail);
        }

        return planDetails;
    }

    [Fact]
    public async Task ExplainQueryPlan_PatientPrefixSearch_UsesNormalizedNameIndex()
    {
        // Act: Run EXPLAIN QUERY PLAN on prefix search with LIKE 'bilal%'
        var queryPlan = await ExplainQueryPlanAsync("SELECT * FROM Patients WHERE NormalizedName LIKE 'bilal%'");

        // Assert: Must use index IX_Patients_NormalizedName and NOT perform SCAN Patients
        Assert.NotEmpty(queryPlan);
        var planText = string.Join("\n", queryPlan);

        Assert.Contains("USING INDEX IX_Patients_NormalizedName", planText);
        Assert.DoesNotContain("SCAN Patients", planText);
    }

    [Fact]
    public async Task ExplainQueryPlan_MedicinePrefixSearch_UsesNormalizedNameIndex()
    {
        // Act: Run EXPLAIN QUERY PLAN on prefix search with LIKE 'amox%'
        var queryPlan = await ExplainQueryPlanAsync("SELECT * FROM Medicines WHERE NormalizedName LIKE 'amox%'");

        // Assert: Must use index IX_Medicines_NormalizedName and NOT perform SCAN Medicines
        Assert.NotEmpty(queryPlan);
        var planText = string.Join("\n", queryPlan);

        Assert.Contains("USING INDEX IX_Medicines_NormalizedName", planText);
        Assert.DoesNotContain("SCAN Medicines", planText);
    }

    [Fact]
    public async Task MedicineValidation_FormIsRequired_And_UpdateValidationRejectsEmptyName()
    {
        // Arrange
        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var service = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance);

        // Act 1: Attempt to create medicine without Form
        var createResultNoForm = await service.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Amoxicillin",
            Form = "" // Blank form
        });

        Assert.False(createResultNoForm.IsSuccess);
        Assert.Contains("Dosage form is required", createResultNoForm.ErrorMessage);

        // Act 2: Create a valid medicine
        var createValidResult = await service.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Amoxicillin",
            Form = "Capsule",
            Strength = "500mg"
        });
        Assert.True(createValidResult.IsSuccess);
        var createdId = createValidResult.Value!.Id;

        // Act 3: Attempt to update medicine with null/whitespace name
        var updateResultNoName = await service.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = createdId,
            Name = "   ",
            Form = "Capsule"
        });
        Assert.False(updateResultNoName.IsSuccess);
        Assert.Contains("Medicine name is required", updateResultNoName.ErrorMessage);

        // Act 4: Attempt to update medicine with empty Form
        var updateResultNoForm = await service.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = createdId,
            Name = "Amoxicillin",
            Form = ""
        });
        Assert.False(updateResultNoForm.IsSuccess);
        Assert.Contains("Dosage form is required", updateResultNoForm.ErrorMessage);
    }
}
