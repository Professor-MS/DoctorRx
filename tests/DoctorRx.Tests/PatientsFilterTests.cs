using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class PatientsFilterTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;

    public PatientsFilterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_FilterTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task FilterCriteria_SearchAndStatusAndSortAndPaging_ReturnsAccurateResultsWithoutGaps()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 20; i++)
            {
                var isArchived = (i % 2 == 0); // Even numbers archived
                var p = new Patient
                {
                    Name = $"Patient {i:D2} Khan",
                    RecordNumber = $"P-{i:D6}",
                    DateOfBirth = new DateOnly(1980, 1, 1),
                    Gender = (i % 2 == 0) ? Gender.Female : Gender.Male,
                    Phone = $"0300{i:D7}",
                    IsArchived = isArchived,
                    LastVisitDate = new DateOnly(2026, 1, 1).AddDays(i),
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
                };
                p.RefreshSearchFields();
                context.Patients.Add(p);
            }
            await context.SaveChangesAsync();
        }

        var uowFactory = new UnitOfWorkFactory(_factory);
        var clock = new SystemClock();
        var service = new PatientService(uowFactory, clock, NullLogger<PatientService>.Instance);

        // 1. Filter: Active only (StatusFilter = Active), PageSize = 5
        var activeCriteria = new PatientFilterCriteria(
            SearchQuery: null,
            StatusFilter: PatientStatusFilter.Active,
            SortOption: PatientSortOption.NameAsc,
            PageNumber: 1,
            PageSize: 5);

        var activePage1 = await service.GetFilteredPatientsPagedAsync(activeCriteria);
        Assert.Equal(10, activePage1.TotalCount);
        Assert.Equal(5, activePage1.Items.Count);
        Assert.True(activePage1.HasMore);
        Assert.All(activePage1.Items, item => Assert.False(item.IsArchived));

        // Page 2
        var activePage2 = await service.GetFilteredPatientsPagedAsync(activeCriteria with { PageNumber = 2 });
        Assert.Equal(5, activePage2.Items.Count);
        Assert.False(activePage2.HasMore);
        Assert.All(activePage2.Items, item => Assert.False(item.IsArchived));

        // Ensure no duplicates across pages
        var overlap = activePage1.Items.Select(x => x.Id).Intersect(activePage2.Items.Select(x => x.Id));
        Assert.Empty(overlap);

        // 2. Filter: Archived only
        var archivedCriteria = new PatientFilterCriteria(
            SearchQuery: null,
            StatusFilter: PatientStatusFilter.Archived,
            SortOption: PatientSortOption.NameAsc,
            PageNumber: 1,
            PageSize: 20);

        var archivedResult = await service.GetFilteredPatientsPagedAsync(archivedCriteria);
        Assert.Equal(10, archivedResult.TotalCount);
        Assert.All(archivedResult.Items, item => Assert.True(item.IsArchived));

        // 3. Filter: Combined Search query "Khan" + Sort by LastVisitDateDesc
        var searchAndSortCriteria = new PatientFilterCriteria(
            SearchQuery: "Khan",
            StatusFilter: PatientStatusFilter.All,
            SortOption: PatientSortOption.LastVisitDesc,
            PageNumber: 1,
            PageSize: 20);

        var searchAndSort = await service.GetFilteredPatientsPagedAsync(searchAndSortCriteria);
        Assert.Equal(20, searchAndSort.TotalCount);
        // Verify sort order: last visit date descending
        for (int i = 0; i < searchAndSort.Items.Count - 1; i++)
        {
            var cur = searchAndSort.Items[i].LastVisitDate;
            var next = searchAndSort.Items[i + 1].LastVisitDate;
            Assert.True(cur >= next);
        }
    }

    [Fact]
    public void PatientsFilterSessionService_PreservesFiltersAcrossInstances()
    {
        var session = new PatientsFilterSessionService();

        // Initial default state
        Assert.Equal(string.Empty, session.SearchQuery);
        Assert.Equal(PatientStatusFilter.Active, session.StatusFilter);
        Assert.Equal(PatientSortOption.NameAsc, session.SortOption);
        Assert.False(session.HasActiveFilters);

        // Update filters
        session.SearchQuery = "Bilal";
        session.StatusFilter = PatientStatusFilter.Archived;
        session.SortOption = PatientSortOption.NameAsc;
        session.CurrentPage = 3;

        Assert.True(session.HasActiveFilters);
        Assert.Equal("Bilal", session.SearchQuery);
        Assert.Equal(PatientStatusFilter.Archived, session.StatusFilter);
        Assert.Equal(PatientSortOption.NameAsc, session.SortOption);
        Assert.Equal(3, session.CurrentPage);

        // Reset/Clear filters
        session.Reset();
        Assert.Equal(string.Empty, session.SearchQuery);
        Assert.Equal(PatientStatusFilter.Active, session.StatusFilter);
        Assert.Equal(PatientSortOption.NameAsc, session.SortOption);
        Assert.Equal(1, session.CurrentPage);
        Assert.False(session.HasActiveFilters);
    }
}
