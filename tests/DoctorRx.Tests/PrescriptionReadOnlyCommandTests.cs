using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Application.Services;
using DoctorRx.Domain.Entities;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Domain.Models;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

/// <summary>
/// Intercepts ALL three DbCommand execution paths (Reader, NonQuery, Scalar)
/// and counts SaveChanges invocations. A read-only operation must produce zero
/// NonSelectCommands and zero SaveChanges calls.
/// </summary>
public class CommandLoggingInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
{
    public List<string> ExecutedCommands { get; } = new();
    public List<string> NonSelectCommands { get; } = new();
    public List<string> ScalarCommands { get; } = new();
    public int SaveChangesCount { get; private set; }

    // --- Reader (SELECT) ---
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        RecordCommand(command.CommandText, isMutation: false);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command.CommandText, isMutation: false);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    // --- Scalar (SELECT ... returning single value, e.g. last_insert_rowid()) ---
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        RecordCommand(command.CommandText, isMutation: false);
        ScalarCommands.Add(command.CommandText.Trim());
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command.CommandText, isMutation: false);
        ScalarCommands.Add(command.CommandText.Trim());
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    // --- NonQuery (INSERT / UPDATE / DELETE) ---
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        RecordCommand(command.CommandText, isMutation: true);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command.CommandText, isMutation: true);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    // --- SaveChanges tracking ---
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SaveChangesCount++;
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return ValueTask.FromResult(result);
    }

    public void SavedChanges(SaveChangesCompletedEventData eventData, int result) { }
    public ValueTask SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void SaveChangesFailed(DbContextErrorEventData eventData) { }
    public ValueTask SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    private void RecordCommand(string sql, bool isMutation)
    {
        if (string.IsNullOrWhiteSpace(sql)) return;
        var trimmed = sql.Trim();
        ExecutedCommands.Add(trimmed);

        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase))
        {
            NonSelectCommands.Add(trimmed);
        }
    }

    public void Reset()
    {
        ExecutedCommands.Clear();
        NonSelectCommands.Clear();
        ScalarCommands.Clear();
        SaveChangesCount = 0;
    }

    // Summarizes all mutation evidence for assert failure messages
    public string MutationSummary() =>
        $"NonSelect: {NonSelectCommands.Count}, SaveChanges: {SaveChangesCount}\n" +
        string.Join("\n", NonSelectCommands.Select(c => $"  NONSELECT: {c.Substring(0, Math.Min(120, c.Length))}"));
}

public class PrescriptionReadOnlyCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly IDbContextFactory<DoctorRxDbContext> _factory;
    private readonly CommandLoggingInterceptor _interceptor;
    private readonly IClock _clock;
    private int _patientId;
    private int _doctorId;
    private int _finalizedRxId;
    private int _supersededRxId;
    private int _replacementRxId;

    public PrescriptionReadOnlyCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_CmdTests_" + Guid.NewGuid().ToString("N"));
        _appPaths = new TestAppPaths(_testDir);
        _interceptor = new CommandLoggingInterceptor();
        _clock = new SystemClock();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _appPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite(builder.ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor(), _interceptor)
            .Options;

        _factory = new TestDbContextFactory(options);

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        migrator.MigrateDatabaseAsync().GetAwaiter().GetResult();

        SeedDataAsync().GetAwaiter().GetResult();
        _interceptor.Reset();
    }

    private async Task SeedDataAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var doc = new Doctor
        {
            Name = "Dr. Tariq Mahmood",
            Qualification = "MBBS, MD",
            RegistrationNumber = "PMC-12345",
            Specialization = "Cardiology",
            ClinicName = "Heart Care Clinic",
            IsActive = true
        };
        var pat = new Patient
        {
            RecordNumber = "P-100200",
            Name = "Muhammad Ali",
            NormalizedName = "muhammad ali",
            Phone = "+92 300 1234567",
            PhoneDigits = "03001234567",
            DateOfBirth = new DateOnly(1980, 5, 10),
            Gender = Gender.Male
        };
        ctx.Doctors.Add(doc);
        ctx.Patients.Add(pat);
        await ctx.SaveChangesAsync();
        _doctorId = doc.Id;
        _patientId = pat.Id;

        // Rx 1: Finalized
        var rx1 = Prescription.CreateFinalized(
            "RX-20261013-000001",
            pat.Id,
            doc.Id,
            DateOnly.FromDateTime(DateTime.Today),
            doc.ToSnapshot(),
            pat.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
            DateTime.UtcNow,
            chiefComplaints: "Chest pain");
        rx1.AddMedicine(new PrescriptionMedicine
        {
            MedicineName = "Aspirin",
            Form = "Tablet",
            Strength = "75 mg",
            Dose = "1 tab",
            Frequency = "Once daily",
            Route = "Oral",
            Duration = "30 days",
            GenericName = "Acetylsalicylic acid"
        });
        ctx.Prescriptions.Add(rx1);
        await ctx.SaveChangesAsync();
        _finalizedRxId = rx1.Id;

        // Rx 2: Superseded by Rx 3 (Amendment)
        var rx2 = Prescription.CreateFinalized(
            "RX-20261013-000002",
            pat.Id,
            doc.Id,
            DateOnly.FromDateTime(DateTime.Today),
            doc.ToSnapshot(),
            pat.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
            DateTime.UtcNow,
            chiefComplaints: "Hypertension");
        rx2.AddMedicine(new PrescriptionMedicine
        {
            MedicineName = "Lisinopril",
            Form = "Tablet",
            Strength = "10 mg",
            Dose = "1 tab",
            Frequency = "Once daily",
            Route = "Oral",
            Duration = "14 days"
        });
        ctx.Prescriptions.Add(rx2);
        await ctx.SaveChangesAsync();
        _supersededRxId = rx2.Id;

        var rx3 = Prescription.CreateFinalized(
            "RX-20261013-000002-A1",
            pat.Id,
            doc.Id,
            DateOnly.FromDateTime(DateTime.Today),
            doc.ToSnapshot(),
            pat.ToSnapshot(DateOnly.FromDateTime(DateTime.Today)),
            DateTime.UtcNow,
            parentPrescriptionId: rx2.Id,
            amendmentNumber: 1,
            chiefComplaints: "Hypertension (amended)");
        rx3.AddMedicine(new PrescriptionMedicine
        {
            MedicineName = "Lisinopril",
            Form = "Tablet",
            Strength = "20 mg",
            Dose = "1 tab",
            Frequency = "Once daily",
            Route = "Oral",
            Duration = "30 days"
        });
        rx2.MarkSuperseded(DateTime.UtcNow);
        ctx.Prescriptions.Add(rx3);
        await ctx.SaveChangesAsync();
        _replacementRxId = rx3.Id;

        // Index the seeded prescriptions
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        await repairService.RepairIndexAsync();
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
        catch { }
    }

    [Fact]
    public async Task CommandInterceptor_ZeroNonSelectStatements_DuringHistoryOpen_Search_Page_AndDetailOpen()
    {
        var uowFactory = new UnitOfWorkFactory(_factory);
        var rxService = new PrescriptionService(uowFactory, _clock, NullLogger<PrescriptionService>.Instance);
        var stubNav = new TestNavService();
        var stubDialog = new TestDialogService();
        var session = new PrescriptionHistoryFilterSessionService();
        var stubLauncher = new TestLauncher();

        var historyVm = new PrescriptionHistoryViewModel(rxService, stubNav, stubDialog, session, stubLauncher);

        _interceptor.Reset();

        // 1. Open Prescription History
        await historyVm.InitializeAsync();
        Assert.NotEmpty(historyVm.Prescriptions);
        // Assert zero mutations across all three paths (NonQuery + Scalar writes + SaveChanges)
        Assert.True(_interceptor.NonSelectCommands.Count == 0, $"Step 1: {_interceptor.MutationSummary()}");
        Assert.Equal(0, _interceptor.SaveChangesCount);

        // 2. Perform Search
        historyVm.SearchText = "aspirin";
        await historyVm.LoadPrescriptionsAsync(resetPage: true);
        Assert.NotEmpty(historyVm.Prescriptions);
        Assert.True(_interceptor.NonSelectCommands.Count == 0, $"Step 2: {_interceptor.MutationSummary()}");
        Assert.Equal(0, _interceptor.SaveChangesCount);

        // 3. Page navigation (load more pages without resetting)
        historyVm.SearchText = string.Empty;
        await historyVm.LoadPrescriptionsAsync(resetPage: true);
        await historyVm.LoadPrescriptionsAsync(resetPage: false);
        Assert.True(_interceptor.NonSelectCommands.Count == 0, $"Step 3: {_interceptor.MutationSummary()}");
        Assert.Equal(0, _interceptor.SaveChangesCount);

        // 4. Open Prescription Detail
        var detailVm = new PrescriptionDetailViewModel(rxService, stubNav, stubDialog, stubLauncher);
        await detailVm.InitializeAsync(_finalizedRxId);
        Assert.NotNull(detailVm.Prescription);
        Assert.True(_interceptor.NonSelectCommands.Count == 0, $"Step 4: {_interceptor.MutationSummary()}");
        Assert.Equal(0, _interceptor.SaveChangesCount);

        // 5. Open Superseded Prescription Detail (loads replacement prescription)
        await detailVm.InitializeAsync(_supersededRxId);
        Assert.NotNull(detailVm.Prescription);
        Assert.NotNull(detailVm.ReplacementPrescription);
        Assert.Equal("RX-20261013-000002-A1", detailVm.ReplacementPrescription.PrescriptionNumber);
        Assert.True(_interceptor.NonSelectCommands.Count == 0, $"Step 5: {_interceptor.MutationSummary()}");
        Assert.Equal(0, _interceptor.SaveChangesCount);
    }

    [Fact]
    public async Task ReplacedBy_SingleQuery_Projection_NoNPlusOne()
    {
        var uowFactory = new UnitOfWorkFactory(_factory);
        var rxService = new PrescriptionService(uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        _interceptor.Reset();

        // Search with IncludeReplaced = true so superseded item is included
        var result = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            IncludeReplaced: true,
            PageSize: 50
        ));

        Assert.NotEmpty(result.Items);
        var supersededItem = result.Items.FirstOrDefault(i => i.Id == _supersededRxId);
        Assert.NotNull(supersededItem);
        Assert.Equal("RX-20261013-000002-A1", supersededItem.ReplacedByNumber);

        // Verify query count: exactly 1 SELECT for items projection + 1 SELECT for total count
        var selectCommands = _interceptor.ExecutedCommands
            .Where(c => c.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Should be exactly 2 queries total (items + count), NOT N+1 queries per item!
        Assert.Equal(2, selectCommands.Count);
        Assert.Empty(_interceptor.NonSelectCommands);
    }

    [Fact]
    public async Task PartiallyIndexedState_PrescriptionsWithoutTokens_QuerySucceeds_AndRepairIndexesThem()
    {
        // Insert a prescription directly via raw SQL without tokens (simulating legacy / partially indexed DB)
        int unindexedId;
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var conn = ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Prescriptions 
                (PrescriptionNumber, PatientId, DoctorId, PrescriptionDate, Doctor_Name, Doctor_Qualification, Doctor_RegistrationNumber, Doctor_Specialization, Doctor_ClinicName, Patient_Name, Patient_Gender, Patient_AgeText, Status, FinalizedAtUtc, AmendmentNumber, Version, CreatedAtUtc, IsSealed)
                VALUES ('RX-20261013-999999', $patId, $docId, '2026-10-13', 'Dr. Tariq', 'MBBS', 'PMC-12345', 'Cardiology', 'Heart Care', 'Muhammad Ali', 1, '40 yrs', 1, '2026-10-13T10:00:00Z', 0, 1, '2026-10-13T10:00:00Z', 0);
                SELECT last_insert_rowid();";
            cmd.Parameters.Add(new SqliteParameter("$patId", _patientId));
            cmd.Parameters.Add(new SqliteParameter("$docId", _doctorId));
            unindexedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        var uowFactory = new UnitOfWorkFactory(_factory);
        var rxService = new PrescriptionService(uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        // 1. History query still succeeds without throwing in partially indexed database
        var pagedResult = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(PageSize: 50));
        Assert.Contains(pagedResult.Items, i => i.Id == unindexedId);

        // 2. Repair job identifies and repairs the missing prescription tokens
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        var report = await repairService.RepairIndexAsync();
        Assert.True(report.PrescriptionsRepaired >= 1);

        // 3. Now searchable by token
        var tokenResult = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "999999"));
        Assert.Contains(tokenResult.Items, i => i.Id == unindexedId);
    }

    [Fact]
    public async Task DigitOnlyRecordNumber_Rule_PreservesPatientAndRxPrefixSearching()
    {
        var uowFactory = new UnitOfWorkFactory(_factory);
        var rxService = new PrescriptionService(uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        // Digits-only query "100200" matches patient record number P-100200
        var resultByMrn = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "100200"));
        Assert.NotEmpty(resultByMrn.Items);

        // Digits query "000001" matches prescription number part RX-20261013-000001
        var resultByRxNum = await rxService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "000001"));
        Assert.Contains(resultByRxNum.Items, i => i.Id == _finalizedRxId);
    }

    [Fact]
    public async Task AmendmentTokens_IndexAmendmentSuffixParts()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var tokens = await ctx.PrescriptionSearchTokens
            .Where(t => t.PrescriptionId == _replacementRxId)
            .Select(t => t.Token)
            .ToListAsync();

        // Must index "a1", "000002", "20261013", etc.
        Assert.Contains("a1", tokens);
        Assert.Contains("000002", tokens);
        Assert.Contains("20261013", tokens);
    }

    private class TestNavService : INavigationService
    {
        public ViewModelBase? CurrentViewModel => null;
        public NavigationDestination CurrentDestination { get; private set; } = NavigationDestination.PrescriptionHistory;
        public event Action<ViewModelBase>? CurrentViewModelChanged { add { } remove { } }
        public void NavigateTo(NavigationDestination destination, object? parameter = null)
        {
            CurrentDestination = destination;
        }
    }

    private class TestDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool? ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel") => true;
    }

    private class TestLauncher : IPrintPreviewLauncher
    {
        public void Launch(PrescriptionDetailDto prescription) { }
    }
}
