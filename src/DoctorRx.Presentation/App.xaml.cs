using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DoctorRx.Application;
using DoctorRx.Application.Interfaces;
using DoctorRx.Infrastructure;
using DoctorRx.Presentation.Services;
using DoctorRx.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register Global Exception Handlers
        SetupGlobalExceptionHandling();

        try
        {
            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureServices((context, services) =>
                {
                    // Register Clean Architecture layers
                    services.AddApplication();
                    services.AddInfrastructure();

                    // Register Presentation services
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IDialogService, DialogService>();

                    // Register ViewModels
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<PatientsViewModel>();
                    services.AddTransient<NewPrescriptionViewModel>();
                    services.AddTransient<PrescriptionHistoryViewModel>();
                    services.AddTransient<MedicinesViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Register Windows
                    services.AddSingleton<MainWindow>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();
                })
                .Build();

            await _host.StartAsync();

            // Run database migrations/initialization
            using (var scope = _host.Services.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();
            }

            // Launch the Main Window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start DoctorRx workstation.\n\nError: {ex.Message}",
                "DoctorRx Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void SetupGlobalExceptionHandling()
    {
        // 1. Dispatcher Unhandled Exceptions (UI Thread)
        DispatcherUnhandledException += (s, args) =>
        {
            args.Handled = true;
            ShowSafeErrorDialog("An unexpected interface error occurred. Your work in the database remains safe.", args.Exception);
        };

        // 2. AppDomain Unhandled Exceptions (Non-UI threads)
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                ShowSafeErrorDialog("A critical system error occurred. Please restart DoctorRx.", ex);
            }
        };

        // 3. TaskScheduler Unobserved Exceptions
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved();
        };
    }

    private static void ShowSafeErrorDialog(string userFriendlyMessage, Exception ex)
    {
        // Avoid leaking raw stack traces to the clinic user; provide friendly notice and log message
        MessageBox.Show(
            $"{userFriendlyMessage}\n\nNotice: {ex.Message}",
            "DoctorRx System Notice",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
