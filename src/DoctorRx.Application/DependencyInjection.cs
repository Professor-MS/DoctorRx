using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IMedicineService, MedicineService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
