using System;
using DoctorRx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DoctorRx.Tests;

public class ModelVsMigrationsTests
{
    [Fact]
    public void DatabaseModel_MatchesLatestMigrationsModelSnapshot()
    {
        // Arrange: Create a DbContext instance
        var options = new DbContextOptionsBuilder<DoctorRxDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new DoctorRxDbContext(options);

        var modelDiffer = context.GetService<IMigrationsModelDiffer>();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot;

        Assert.NotNull(snapshot);

        // Finalize / initialize snapshot model if needed
        var snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model);

        // Get current active design-time model from the DbContext
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        // Act: Compute differences between snapshot model and current active DbContext model
        var differences = modelDiffer.GetDifferences(
            snapshotModel.GetRelationalModel(),
            designTimeModel.GetRelationalModel());

        // Assert: There must be ZERO differences. If someone changes entities or DbContext without a migration, this fails!
        Assert.Empty(differences);
    }
}
