using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class SearchEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public SearchEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_SearchEngine_" + Guid.NewGuid().ToString("N"));
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
        }
    }

    [Fact]
    public async Task Search_MiddleOrLastWord_FindsPatient()
    {
        // Patient named "Muhammad Ali Khan"
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = new Patient
            {
                Name = "Muhammad Ali Khan",
                RecordNumber = "P-000001",
                DateOfBirth = new DateOnly(1988, 3, 15),
                Gender = Gender.Male,
                Phone = "03001234567"
            };
            p.RefreshSearchFields();
            context.Patients.Add(p);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new PatientRepository(context);

            // Search for "Ali"
            var resultsAli = await repo.SearchAsync("Ali");
            Assert.NotEmpty(resultsAli);
            Assert.Equal("Muhammad Ali Khan", resultsAli[0].Name);

            // Search for "Khan"
            var resultsKhan = await repo.SearchAsync("Khan");
            Assert.NotEmpty(resultsKhan);
            Assert.Equal("Muhammad Ali Khan", resultsKhan[0].Name);

            // Multi-token out of order: "Khan Muhammad"
            var resultsOutOfOrder = await repo.SearchAsync("Khan Muhammad");
            Assert.NotEmpty(resultsOutOfOrder);
            Assert.Equal("Muhammad Ali Khan", resultsOutOfOrder[0].Name);
        }
    }

    [Fact]
    public async Task Search_RecordNumberFormats_ResolvesConsistently()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = new Patient
            {
                Name = "Tariq Mahmood",
                RecordNumber = "P-000012",
                DateOfBirth = new DateOnly(1975, 6, 20),
                Gender = Gender.Male,
                Phone = "03339876543"
            };
            p.RefreshSearchFields();
            context.Patients.Add(p);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new PatientRepository(context);

            // Format 1: Exact "P-000012"
            var r1 = await repo.SearchAsync("P-000012");
            Assert.Single(r1);

            // Format 2: Lowercase "p-000012"
            var r2 = await repo.SearchAsync("p-000012");
            Assert.Single(r2);

            // Format 3: Raw integer "12"
            var r3 = await repo.SearchAsync("12");
            Assert.Single(r3);

            // Format 4: Short prefix "P-12"
            var r4 = await repo.SearchAsync("P-12");
            Assert.Single(r4);

            // Format 5: Zero-padded integer "000012"
            var r5 = await repo.SearchAsync("000012");
            Assert.Single(r5);
        }
    }

    [Fact]
    public async Task Search_PhoneSuffixAndDigitVariants_MatchesConsistently()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = new Patient
            {
                Name = "Fatima Bibi",
                RecordNumber = "P-000025",
                DateOfBirth = new DateOnly(1992, 10, 5),
                Gender = Gender.Female,
                Phone = "+92 333 4567890"
            };
            p.RefreshSearchFields();
            context.Patients.Add(p);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new PatientRepository(context);

            // Local 0333 prefix
            var r1 = await repo.SearchAsync("03334567890");
            Assert.Single(r1);

            // Suffix digits (last 7 digits)
            var r2 = await repo.SearchAsync("4567890");
            Assert.Single(r2);

            // Middle/suffix digits (at least 3 digits)
            var r3 = await repo.SearchAsync("67890");
            Assert.Single(r3);

            // Eastern Arabic-Indic numerals for "4567890" -> "۴۵۶۷۸۹۰"
            var r4 = await repo.SearchAsync("۴۵۶۷۸۹۰");
            Assert.Single(r4);
        }
    }

    [Fact]
    public async Task Search_UrduNameVariantsAndDiacritics_Matches()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p = new Patient
            {
                Name = "عائشہ بی بی", // Aisha Bibi with Urdu Heh
                RecordNumber = "P-000030",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Gender = Gender.Female,
                Phone = "03215554433"
            };
            p.RefreshSearchFields();
            context.Patients.Add(p);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new PatientRepository(context);

            // Exact Urdu query
            var r1 = await repo.SearchAsync("عائشہ");
            Assert.NotEmpty(r1);

            // Variant with Arabic Teh Marbuta or diacritics: "عَائِشَة"
            var r2 = await repo.SearchAsync("عَائِشَة");
            Assert.NotEmpty(r2);

            // Second word "بی بی"
            var r3 = await repo.SearchAsync("بی بی");
            Assert.NotEmpty(r3);
        }
    }

    [Fact]
    public async Task Search_SingleCharacter_ReturnsMatchingPrefixes()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var p1 = new Patient { Name = "Bilal", RecordNumber = "P-000101", DateOfBirth = new DateOnly(1990, 1, 1), Gender = Gender.Male };
            var p2 = new Patient { Name = "Zainab", RecordNumber = "P-000102", DateOfBirth = new DateOnly(1990, 1, 1), Gender = Gender.Female };
            p1.RefreshSearchFields();
            p2.RefreshSearchFields();
            context.Patients.AddRange(p1, p2);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new PatientRepository(context);

            var rB = await repo.SearchAsync("b");
            Assert.Contains(rB, p => p.Name == "Bilal");
            Assert.DoesNotContain(rB, p => p.Name == "Zainab");

            var rZ = await repo.SearchAsync("z");
            Assert.Contains(rZ, p => p.Name == "Zainab");
            Assert.DoesNotContain(rZ, p => p.Name == "Bilal");
        }
    }

    [Fact]
    public async Task MedicineSearch_MatchesBrandGenericAndWords()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var med = new Medicine
            {
                Name = "Panadol Extra",
                GenericName = "Paracetamol + Caffeine",
                Form = "Tablet",
                Strength = "500mg/65mg",
                UsageCount = 20,
                IsActive = true
            };
            med.RefreshSearchFields();
            context.Medicines.Add(med);
            await context.SaveChangesAsync();
        }

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var repo = new MedicineRepository(context);

            // Brand name search
            var r1 = await repo.SearchAsync("Panadol");
            Assert.Single(r1);

            // Second brand word search
            var r2 = await repo.SearchAsync("Extra");
            Assert.Single(r2);

            // Generic name search
            var r3 = await repo.SearchAsync("Paracetamol");
            Assert.Single(r3);

            // Second generic word search
            var r4 = await repo.SearchAsync("Caffeine");
            Assert.Single(r4);
        }
    }
}
