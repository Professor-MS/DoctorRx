using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DoctorRx.Application.Services;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoctorRx.Tests;

public class MedicineSearchScaleTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;
    private readonly TestDbContextFactory _factory;

    public MedicineSearchScaleTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"DoctorRx_Scale_{Guid.NewGuid():N}.db");
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
    public async Task TwentyThousandMedicines_SearchWithLimit_Under100Ms_AndExplainQueryPlanUsesIndex()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            await ctx.Database.MigrateAsync();
        }

        // 1. Bulk insert 20,000 medicines and tokens
        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();

            using var medCmd = conn.CreateCommand();
            medCmd.Transaction = trans;
            medCmd.CommandText = @"
                INSERT INTO Medicines (
                    Id, Name, NormalizedName, GenericName, Form, Strength, IsActive, UsageCount, CreatedAtUtc
                ) VALUES (
                    @id, @name, @normName, @genName, @form, @str, 1, @usage, '2026-10-10 12:00:00'
                );";
            var pId = medCmd.Parameters.Add("@id", SqliteType.Integer);
            var pName = medCmd.Parameters.Add("@name", SqliteType.Text);
            var pNormName = medCmd.Parameters.Add("@normName", SqliteType.Text);
            var pGenName = medCmd.Parameters.Add("@genName", SqliteType.Text);
            var pForm = medCmd.Parameters.Add("@form", SqliteType.Text);
            var pStr = medCmd.Parameters.Add("@str", SqliteType.Text);
            var pUsage = medCmd.Parameters.Add("@usage", SqliteType.Integer);

            using var tokCmd = conn.CreateCommand();
            tokCmd.Transaction = trans;
            tokCmd.CommandText = @"
                INSERT INTO MedicineSearchTokens (
                    MedicineId, Token, TokenType
                ) VALUES (
                    @medId, @tok, 0
                );";
            var ptMedId = tokCmd.Parameters.Add("@medId", SqliteType.Integer);
            var ptTok = tokCmd.Parameters.Add("@tok", SqliteType.Text);

            string[] prefixes = { "amox", "cipro", "parac", "omep", "azith", "metfo", "atorv", "losar", "pantop", "cef" };

            for (int i = 1; i <= 20000; i++)
            {
                var prefix = prefixes[i % prefixes.Length];
                var name = $"{prefix}med_{i}";
                var normName = name.ToLowerInvariant();
                var genName = $"generic_{prefix}_{i}";

                pId.Value = i;
                pName.Value = name;
                pNormName.Value = normName;
                pGenName.Value = genName;
                pForm.Value = (i % 2 == 0) ? "Tablet" : "Capsule";
                pStr.Value = $"{100 + (i % 10) * 50} mg";
                pUsage.Value = i % 50;
                await medCmd.ExecuteNonQueryAsync();

                ptMedId.Value = i;
                ptTok.Value = normName;
                await tokCmd.ExecuteNonQueryAsync();
            }

            await trans.CommitAsync();

            // Run ANALYZE so SQLite query planner has accurate table statistics
            using var analyzeCmd = conn.CreateCommand();
            analyzeCmd.CommandText = "ANALYZE;";
            await analyzeCmd.ExecuteNonQueryAsync();
        }

        // 2. EXPLAIN QUERY PLAN verification
        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var explainCmd = conn.CreateCommand();
            explainCmd.CommandText = "EXPLAIN QUERY PLAN SELECT MedicineId FROM MedicineSearchTokens WHERE Token LIKE 'amox%';";

            var planLines = new List<string>();
            using (var reader = await explainCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    // detail column is index 3
                    planLines.Add(reader.GetString(3));
                }
            }

            var planText = string.Join("\n", planLines);
            // Verify SQLite query planner uses the covering index on (Token, MedicineId)
            Assert.Contains("IX_MedicineSearchTokens_Token_MedicineId", planText);
        }

        // 3. Search under 100 ms with LIMIT
        var uowFactory = new UnitOfWorkFactory(_factory);
        var searchService = new MedicineSearchService(uowFactory, NullLogger<MedicineSearchService>.Instance);

        // Warm up JIT
        _ = await searchService.SearchAsync("amox", maxResults: 50);

        // Benchmark timed search
        var sw = Stopwatch.StartNew();
        var results = await searchService.SearchAsync("amox", maxResults: 50);
        sw.Stop();

        Assert.NotEmpty(results);
        Assert.True(results.Count <= 50);
        Assert.True(sw.ElapsedMilliseconds < 100, $"Expected search under 100 ms for 20,000 medicines with LIMIT, but took {sw.ElapsedMilliseconds} ms.");
    }
}
