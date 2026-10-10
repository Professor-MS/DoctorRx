using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineSearchTokenLifecycleTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;
    private readonly TestDbContextFactory _factory;

    public MedicineSearchTokenLifecycleTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"DoctorRx_Tokens_{Guid.NewGuid():N}.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _tempDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        _connectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
        _factory = new TestDbContextFactory(options);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(_tempDbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task SearchTokens_MaintainedOnCreate_Rename_AndDeactivate_InSameTransaction()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        var clock = new SystemClock();
        var uowFactory = new UnitOfWorkFactory(_factory);
        var searchService = new MedicineSearchService(uowFactory, NullLogger<MedicineSearchService>.Instance);
        var medicineService = new MedicineService(uowFactory, clock, NullLogger<MedicineService>.Instance, searchService);

        // 1. CREATE: Add medicine "Amoxil 500mg"
        var createResult = await medicineService.CreateMedicineAsync(new CreateMedicineDto
        {
            Name = "Amoxil",
            GenericName = "Amoxicillin",
            Form = "Capsule",
            Strength = "500 mg"
        });
        Assert.True(createResult.IsSuccess);
        var medId = createResult.Value!.Id;

        // Verify tokens created in the same transaction
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var tokens = await ctx.MedicineSearchTokens.Where(t => t.MedicineId == medId).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Token.StartsWith("amoxil"));
        }

        // Verify search finds "Amoxil"
        var initialSearch = await medicineService.SearchMedicinesAsync("Amoxil");
        Assert.Single(initialSearch);
        Assert.Equal("Amoxil", initialSearch[0].Name);

        // 2. EDIT / RENAME: Rename "Amoxil" to "Moxatag"
        var updateResult = await medicineService.UpdateMedicineAsync(new UpdateMedicineDto
        {
            Id = medId,
            Name = "Moxatag",
            GenericName = "Amoxicillin",
            Form = "Capsule",
            Strength = "500 mg",
            IsActive = true
        });
        Assert.True(updateResult.IsSuccess);

        // Verify old tokens removed and new tokens added in same transaction
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var tokens = await ctx.MedicineSearchTokens.Where(t => t.MedicineId == medId).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Token.StartsWith("moxatag"));
            Assert.DoesNotContain(tokens, t => t.Token.StartsWith("amoxil"));
        }

        // Test: The new name is found
        var newSearch = await medicineService.SearchMedicinesAsync("Moxatag");
        Assert.Single(newSearch);
        Assert.Equal("Moxatag", newSearch[0].Name);

        // Test: The old name is NOT found
        var oldSearch = await medicineService.SearchMedicinesAsync("Amoxil");
        Assert.Empty(oldSearch);

        // 3. DEACTIVATE: Deactivate the medicine
        var deleteResult = await medicineService.DeleteMedicineAsync(medId);
        Assert.True(deleteResult.IsSuccess);

        // Test: Deactivated medicines are excluded by default in active searches
        var activeSearch = await medicineService.SearchMedicinesAsync("Moxatag");
        Assert.Empty(activeSearch);

        // Test: Deactivated medicines are included when IncludeInactive = true
        var includeInactiveSearch = await searchService.SearchAsync(new MedicineSearchCriteria
        {
            Query = "Moxatag",
            IncludeInactive = true
        });
        Assert.Single(includeInactiveSearch);
        Assert.False(includeInactiveSearch[0].IsActive);
    }
}
