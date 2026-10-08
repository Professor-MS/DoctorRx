using DoctorRx.Application.Interfaces;
using DoctorRx.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorRx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services are stateless and use IUnitOfWorkFactory for short-lived DbContext lifetimes
        services.AddSingleton<IPatientService, PatientService>();
        services.AddSingleton<IPrescriptionService, PrescriptionService>();
        services.AddSingleton<IMedicineService, MedicineService>();
        services.AddSingleton<IDoctorService, DoctorService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IDraftService, DraftService>();

        return services;
    }
}
