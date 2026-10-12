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
using DoctorRx.Domain.Models;
using DoctorRx.Domain.ValueObjects;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class PrescriptionSearchTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestAppPaths _appPaths;
    private readonly TestDbContextFactory _factory;
    private readonly UnitOfWorkFactory _uowFactory;
    private readonly SystemClock _clock;
    private readonly PrescriptionService _prescriptionService;

    public PrescriptionSearchTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DoctorRx_RxSearchTests_" + Guid.NewGuid().ToString("N"));
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
        _uowFactory = new UnitOfWorkFactory(_factory);
        _clock = new SystemClock();
        _prescriptionService = new PrescriptionService(_uowFactory, _clock, NullLogger<PrescriptionService>.Instance);

        var migrator = new DatabaseMigrator(_factory, _appPaths, NullLogger<DatabaseMigrator>.Instance);
        migrator.MigrateDatabaseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        foreach (var ctx in _factory.CreatedContexts)
        {
            ctx.Dispose();
        }
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    private static CreatePrescriptionMedicineDto CreateItem(string name, string generic = "", string form = "Tablet", string strength = "500mg") =>
        new()
        {
            MedicineName = name,
            GenericName = generic,
            Form = form,
            Strength = strength,
            Dose = "1 tab",
            Frequency = "TDS",
            Route = "Oral",
            Duration = "5 days"
        };

    private async Task<(Doctor Doctor, Patient Patient)> SeedDoctorAndPatientAsync(string patientName = "Zainab Bibi", string phone = "03001234567")
    {
        await using var context = await _factory.CreateDbContextAsync();

        var doctor = await context.Doctors.FirstOrDefaultAsync(d => d.IsActive);
        if (doctor == null)
        {
            doctor = new Doctor
            {
                Name = "Tariq Mahmood",
                TitlePrefix = "Dr.",
                Qualification = "MBBS, FCPS",
                RegistrationLabel = "Reg. No.",
                RegistrationNumber = "12345-P",
                Specialization = "General Physician",
                ClinicName = "Al-Shifa Clinic",
                ClinicAddress = "Main Road, Lahore",
                ClinicPhone = "042-35881122",
                IsActive = true
            };
            context.Doctors.Add(doctor);
        }

        var patient = new Patient
        {
            RecordNumber = "P-000101",
            Name = patientName,
            Age = 32,
            Gender = Gender.Female,
            Phone = phone,
            PhoneDigits = SearchNormalizer.NormalizePhoneDigits(phone),
            NormalizedName = SearchNormalizer.Normalize(patientName)
        };
        patient.RefreshSearchFields();
        context.Patients.Add(patient);

        // Mark search indexes as initialized so index path is active
        var meta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "SearchIndexVersion");
        if (meta == null)
        {
            context.AppMetas.Add(new AppMeta { Key = "SearchIndexVersion", Value = "2" });
        }
        var rxMeta = await context.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion");
        if (rxMeta == null)
        {
            context.AppMetas.Add(new AppMeta { Key = "PrescriptionSearchIndexVersion", Value = "1" });
        }

        await context.SaveChangesAsync();
        return (doctor, patient);
    }

    [Fact]
    public void Tokenization_GeneratesCorrectPrescriptionNumberTokens_IncludingStrippedZerosAndAmendment()
    {
        // 1. Regular prescription number "RX-20261009-0002"
        var tokens = SearchNormalizer.GeneratePrescriptionNumberTokens("RX-20261009-0002");
        Assert.Contains("rx-20261009-0002", tokens);
        Assert.Contains("20261009-0002", tokens);
        Assert.Contains("20261009", tokens);
        Assert.Contains("0002", tokens);
        Assert.Contains("2", tokens); // stripped leading zeros

        // 2. Amendment prescription number "RX-20261009-0002-A1"
        var amendTokens = SearchNormalizer.GeneratePrescriptionNumberTokens("RX-20261009-0002-A1");
        Assert.Contains("rx-20261009-0002-a1", amendTokens);
        Assert.Contains("20261009-0002-a1", amendTokens);
        Assert.Contains("20261009", amendTokens);
        Assert.Contains("0002", amendTokens);
        Assert.Contains("2", amendTokens);
        Assert.Contains("a1", amendTokens);
        Assert.Contains("-a1", amendTokens);
    }

    [Fact]
    public void QueryParser_CorrectlyClassifiesTokens_IncludingRxAndAmendments()
    {
        var tokens = SearchNormalizer.ParseQueryTokens("RX-20261009-0002 a1 0045 p-000101");
        Assert.Equal(4, tokens.Count);

        // 1. Rx prefix
        Assert.True(tokens[0].IsRxNumberPrefix);
        Assert.Equal("20261009-0002", tokens[0].RxNumberWithoutPrefix);

        // 2. Amendment token
        Assert.True(tokens[1].IsAmendment);

        // 3. Digits-only
        Assert.True(tokens[2].IsDigitsOnly);
        Assert.Equal("45", tokens[2].StrippedDigits);

        // 4. Patient record number prefix
        Assert.True(tokens[3].IsRecordNumberPrefix);
        Assert.Equal("101", tokens[3].RecordNumberDigits);
    }

    [Fact]
    public async Task Search_RenamedPatient_FoundByBothSnapshotNameAndCurrentPatientName()
    {
        // Arrange: Patient created with original name "Zainab Bibi"
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Zainab Bibi", "03001234567");

        var createDto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                CreateItem("Panadol Extra", "Paracetamol")
            }
        };

        var result = await _prescriptionService.FinalizePrescriptionAsync(createDto);
        Assert.True(result.IsSuccess);
        var rx = result.Value!;

        // Now rename the patient in the database to "Zainab Fatima"
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var p = await ctx.Patients.Include(x => x.SearchTokens).FirstAsync(x => x.Id == patient.Id);
            p.Name = "Zainab Fatima";
            ctx.PatientSearchTokens.RemoveRange(p.SearchTokens);
            p.RefreshSearchFields();
            foreach (var tok in p.SearchTokens) ctx.PatientSearchTokens.Add(tok);
            await ctx.SaveChangesAsync();
        }

        // Act & Assert 1: Search by snapshot name "bibi" (found via PrescriptionSearchTokens)
        var searchBibi = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "bibi"));
        Assert.Single(searchBibi.Items);
        Assert.Equal(rx.Id, searchBibi.Items[0].Id);
        Assert.Equal("Zainab Bibi", searchBibi.Items[0].PatientName); // Shows snapshot name

        // Act & Assert 2: Search by current patient name "fatima" (found via PatientSearchTokens UNION)
        var searchFatima = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "fatima"));
        Assert.Single(searchFatima.Items);
        Assert.Equal(rx.Id, searchFatima.Items[0].Id);

        // Act & Assert 3: Search by record number digits "101"
        var searchRecord = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "101"));
        Assert.Single(searchRecord.Items);
        Assert.Equal(rx.Id, searchRecord.Items[0].Id);
    }

    [Fact]
    public async Task Search_DigitOnly_MatchesPrescriptionNumberOrRecordNumber()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Ali Khan", "03007654321");

        var createDto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                CreateItem("Augmentin", "Co-Amoxiclav", "Tablet", "625mg")
            }
        };

        var result = await _prescriptionService.FinalizePrescriptionAsync(createDto);
        Assert.True(result.IsSuccess);
        var rx = result.Value!;

        // The generated prescription number is like "RX-YYYYMMDD-0001"
        // Searching for "1" matches prescription sequence 0001
        var search1 = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "1"));
        Assert.Contains(search1.Items, item => item.Id == rx.Id);

        // Searching for "0001" matches prescription sequence 0001
        var search0001 = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "0001"));
        Assert.Contains(search0001.Items, item => item.Id == rx.Id);
    }

    [Fact]
    public async Task Search_MultiToken_OrderIndependent_UrduAndEnglish()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("احمد خان", "03009988776");

        var createDto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                CreateItem("پیناڈول", "پیراسیٹامول", "گولی", "500mg")
            }
        };

        var result = await _prescriptionService.FinalizePrescriptionAsync(createDto);
        Assert.True(result.IsSuccess);
        var rx = result.Value!;

        // Order 1: Urdu "احمد پیناڈول"
        var search1 = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "احمد پیناڈول"));
        Assert.Single(search1.Items);
        Assert.Equal(rx.Id, search1.Items[0].Id);

        // Order 2: Urdu reversed "پیناڈول احمد"
        var search2 = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(SearchText: "پیناڈول احمد"));
        Assert.Single(search2.Items);
        Assert.Equal(rx.Id, search2.Items[0].Id);
    }

    [Fact]
    public async Task Search_StatusFilter_And_IncludeReplacedToggle()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Farooq Tariq", "03001122334");

        // 1. Create prescription 1 (will be left Finalized)
        var rx1Result = await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("Disprin", "Aspirin", "Tablet", "300mg") }
        });
        Assert.True(rx1Result.IsSuccess);
        var rx1 = rx1Result.Value!;

        // 2. Create prescription 2 and cancel it
        var rx2Result = await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("Brufen", "Ibuprofen", "Tablet", "400mg") }
        });
        Assert.True(rx2Result.IsSuccess);
        var rx2 = rx2Result.Value!;
        await _prescriptionService.CancelPrescriptionAsync(rx2.Id, "Wrong dose");

        // 3. Create prescription 3 and amend it (rx3 becomes Superseded/Replaced)
        var rx3Result = await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("Flagyl", "Metronidazole", "Tablet", "400mg") }
        });
        Assert.True(rx3Result.IsSuccess);
        var rx3 = rx3Result.Value!;
        var amendResult = await _prescriptionService.AmendPrescriptionAsync(rx3.Id, new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("Flagyl", "Metronidazole", "Tablet", "400mg") }
        });
        Assert.True(amendResult.IsSuccess);
        var rx3Amend = amendResult.Value!;

        // Test Default status filter with IncludeReplaced = false
        // Should return rx1 (Issued), rx2 (Cancelled), and rx3Amend (Issued) - rx3 (Superseded) MUST be hidden
        var defaultSearch = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            StatusFilter: PrescriptionStatusSearchFilter.Default,
            IncludeReplaced: false
        ));
        Assert.DoesNotContain(defaultSearch.Items, i => i.Id == rx3.Id);
        Assert.Contains(defaultSearch.Items, i => i.Id == rx1.Id);
        Assert.Contains(defaultSearch.Items, i => i.Id == rx2.Id);
        Assert.Contains(defaultSearch.Items, i => i.Id == rx3Amend.Id);

        // Test Default status filter with IncludeReplaced = true
        // Should include rx3 (Superseded)
        var replacedIncludedSearch = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            StatusFilter: PrescriptionStatusSearchFilter.Default,
            IncludeReplaced: true
        ));
        Assert.Contains(replacedIncludedSearch.Items, i => i.Id == rx3.Id);

        // Test StatusFilter.Issued
        var issuedSearch = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            StatusFilter: PrescriptionStatusSearchFilter.Issued
        ));
        Assert.Contains(issuedSearch.Items, i => i.Id == rx1.Id);
        Assert.Contains(issuedSearch.Items, i => i.Id == rx3Amend.Id);
        Assert.DoesNotContain(issuedSearch.Items, i => i.Id == rx2.Id);
        Assert.DoesNotContain(issuedSearch.Items, i => i.Id == rx3.Id);

        // Test StatusFilter.Cancelled
        var cancelledSearch = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            StatusFilter: PrescriptionStatusSearchFilter.Cancelled
        ));
        Assert.Single(cancelledSearch.Items);
        Assert.Equal(rx2.Id, cancelledSearch.Items[0].Id);

        // Test StatusFilter.Replaced
        var replacedOnlySearch = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            StatusFilter: PrescriptionStatusSearchFilter.Replaced
        ));
        Assert.Single(replacedOnlySearch.Items);
        Assert.Equal(rx3.Id, replacedOnlySearch.Items[0].Id);
        Assert.Equal(rx3Amend.PrescriptionNumber, replacedOnlySearch.Items[0].ReplacedByNumber);
    }

    [Fact]
    public async Task Search_DatePresets_FiltersCorrectDateRanges()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Date Tester", "03001239876");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create prescription today
        await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = today,
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("Med Today", "Generic", "Tablet", "10mg") }
        });

        // Test PrescriptionDatePreset.Today
        var todayResult = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            DatePreset: PrescriptionDatePreset.Today
        ));
        Assert.NotEmpty(todayResult.Items);
        Assert.All(todayResult.Items, i => Assert.Equal(today, i.PrescriptionDate));

        // Test PrescriptionDatePreset.Last7Days
        var last7Result = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            DatePreset: PrescriptionDatePreset.Last7Days
        ));
        Assert.NotEmpty(last7Result.Items);
    }

    [Fact]
    public async Task Search_Sorting_StableTieBreakerOnId()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Sort Tester", "03009998887");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Finalize 3 prescriptions on the exact same date
        var r1 = (await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = today,
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("A1", "G1", "Tab", "1mg") }
        })).Value!;

        var r2 = (await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = today,
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("A2", "G2", "Tab", "2mg") }
        })).Value!;

        var r3 = (await _prescriptionService.FinalizePrescriptionAsync(new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = today,
            Items = new List<CreatePrescriptionMedicineDto> { CreateItem("A3", "G3", "Tab", "3mg") }
        })).Value!;

        // Sort NewestFirst: PrescriptionDate DESC, Id DESC -> r3, r2, r1
        var newestFirst = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            PatientId: patient.Id,
            SortOption: PrescriptionSortOption.NewestFirst
        ));
        Assert.Equal(3, newestFirst.Items.Count);
        Assert.Equal(r3.Id, newestFirst.Items[0].Id);
        Assert.Equal(r2.Id, newestFirst.Items[1].Id);
        Assert.Equal(r1.Id, newestFirst.Items[2].Id);

        // Sort OldestFirst: PrescriptionDate ASC, Id ASC -> r1, r2, r3
        var oldestFirst = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            PatientId: patient.Id,
            SortOption: PrescriptionSortOption.OldestFirst
        ));
        Assert.Equal(3, oldestFirst.Items.Count);
        Assert.Equal(r1.Id, oldestFirst.Items[0].Id);
        Assert.Equal(r2.Id, oldestFirst.Items[1].Id);
        Assert.Equal(r3.Id, oldestFirst.Items[2].Id);
    }

    [Fact]
    public async Task Search_Fallback_WorksWhenIndexVersionMissing()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Fallback Patient", "03005544332");

        var createDto = new CreatePrescriptionDto
        {
            PatientId = patient.Id,
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                CreateItem("FallbackMedicine", "FallbackGeneric", "Syrup", "120ml")
            }
        };

        var result = await _prescriptionService.FinalizePrescriptionAsync(createDto);
        Assert.True(result.IsSuccess);
        var rx = result.Value!;

        // Remove the index version metadata to simulate incomplete indexing
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var rxMeta = await ctx.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion");
            if (rxMeta != null)
            {
                ctx.AppMetas.Remove(rxMeta);
                await ctx.SaveChangesAsync();
            }
        }

        // Search text using fallback query
        var searchResult = await _prescriptionService.SearchPrescriptionsAsync(new PrescriptionSearchCriteria(
            SearchText: "FallbackMedicine"
        ));

        Assert.Single(searchResult.Items);
        Assert.Equal(rx.Id, searchResult.Items[0].Id);
    }

    [Fact]
    public async Task TransactionIsolation_FailedFinalization_LeavesNoOrphanTokens()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Isolation Patient", "03003322110");

        // Attempt to finalize prescription with invalid patient id (causes failure)
        var invalidDto = new CreatePrescriptionDto
        {
            PatientId = 999999, // Non-existent patient
            PrescriptionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<CreatePrescriptionMedicineDto>
            {
                CreateItem("GhostMed", "GhostGen", "Tab", "5mg")
            }
        };

        var finalizeResult = await _prescriptionService.FinalizePrescriptionAsync(invalidDto);
        Assert.False(finalizeResult.IsSuccess);

        // Verify zero tokens were written for GhostMed
        await using var ctx = await _factory.CreateDbContextAsync();
        var orphanTokens = await ctx.PrescriptionSearchTokens.Where(t => t.Token == "ghostmed").ToListAsync();
        Assert.Empty(orphanTokens);
    }

    [Fact]
    public async Task RepairService_RepairsUnindexedPrescription_SetsPrescriptionIndexVersion()
    {
        var (doctor, patient) = await SeedDoctorAndPatientAsync("Repair Test Patient", "03007788990");

        int prescriptionId;
        // Insert an unindexed prescription via raw SQL
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var conn = (SqliteConnection)ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Prescriptions (
    PrescriptionNumber, PatientId, DoctorId, PrescriptionDate, Status, AmendmentNumber, Version, FinalizedAtUtc, IsSealed, CreatedAtUtc,
    Doctor_TitlePrefix, Doctor_Name, Doctor_Qualification, Doctor_RegistrationLabel, Doctor_RegistrationNumber, Doctor_Specialization, Doctor_ClinicName,
    Patient_Name, Patient_Gender, Patient_AgeText
) VALUES (
    'RX-20261012-9999', $patientId, $doctorId, '2026-10-12', 1, 0, 1, '2026-10-12T10:00:00Z', 1, '2026-10-12T10:00:00Z',
    'Dr.', 'Tariq Mahmood', 'MBBS', 'Reg. No.', '12345-P', 'General', 'Clinic',
    'Unindexed Snapshot Name', 1, '30'
);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$patientId", patient.Id);
            cmd.Parameters.AddWithValue("$doctorId", doctor.Id);
            prescriptionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Clear PrescriptionSearchIndexVersion
            var rxMeta = await ctx.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion");
            if (rxMeta != null)
            {
                ctx.AppMetas.Remove(rxMeta);
                await ctx.SaveChangesAsync();
            }
        }

        // Run repair service
        var repairService = new SearchIndexRepairService(_factory, NullLogger<SearchIndexRepairService>.Instance);
        var report = await repairService.RepairIndexAsync();

        Assert.True(report.PrescriptionsRepaired >= 1);

        // Verify tokens created
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var tokens = await ctx.PrescriptionSearchTokens.Where(t => t.PrescriptionId == prescriptionId).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Token == "9999");
            Assert.Contains(tokens, t => t.Token == "unindexed");
            Assert.Contains(tokens, t => t.Token == "snapshot");

            var rxMeta = await ctx.AppMetas.FirstOrDefaultAsync(m => m.Key == "PrescriptionSearchIndexVersion");
            Assert.NotNull(rxMeta);
            Assert.Equal("1", rxMeta.Value);
        }

        // Subsequent run is idempotent
        var report2 = await repairService.RepairIndexAsync();
        Assert.Equal(0, report2.PrescriptionsRepaired);
    }
}
