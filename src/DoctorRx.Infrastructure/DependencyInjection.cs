using System;
using System.IO;
using DoctorRx.Application.Interfaces;
using DoctorRx.Domain.Interfaces;
using DoctorRx.Infrastructure.Data;
using DoctorRx.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? databasePath = null)
    {
        // Default database file path in LocalAppData if not explicitly provided
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DoctorRx"
            );

            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            databasePath = Path.Combine(appDataFolder, "doctorrx.db");
        }

        var connectionString = $"Data Source={databasePath}";

        services.AddDbContext<DoctorRxDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<IMedicineRepository, MedicineRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Database Initializer
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }
}
