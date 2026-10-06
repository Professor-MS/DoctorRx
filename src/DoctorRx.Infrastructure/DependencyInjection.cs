using System;
using System.IO;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using DoctorRx.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? databasePath = null)
    {
        var appPaths = new AppPaths(databasePath != null ? Path.GetDirectoryName(databasePath) : null);
        services.AddSingleton<IAppPaths>(appPaths);

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? appPaths.DatabasePath
        };
        var connectionString = connectionStringBuilder.ToString();

        var pragmaInterceptor = new SqlitePragmaInterceptor();
        services.AddSingleton(pragmaInterceptor);

        // Register DbContextFactory for short-lived DbContext lifetimes with PRAGMA interceptor
        services.AddDbContextFactory<DoctorRxDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(pragmaInterceptor);
        });

        // Register Clock
        services.AddSingleton<IClock, SystemClock>();

        // Register UnitOfWorkFactory
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();

        // Register Database Migrator, Seeder & Initializer
        services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();
        services.AddTransient<IDemoDataSeeder, DemoDataSeeder>();
        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }
}
