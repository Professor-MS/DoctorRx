using System;
using System.IO;
using System.Threading;
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
using Serilog;

namespace DoctorRx.Presentation;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\DoctorRx_SingleInstance_Mutex";
    private const string EventName = @"Local\DoctorRx_SingleInstance_Event";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _registeredWait;
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single Instance Check via Local named Mutex
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            // Another instance is already running; signal it to activate its window and exit
            try
            {
                using var activateEvent = EventWaitHandle.OpenExisting(EventName);
                activateEvent.Set();
            }
            catch
            {
                // Event might not exist yet if the first instance is starting up
            }

            Shutdown(0);
            return;
        }

        // Set up event wait handle so a secondary instance can activate this window
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (state, timedOut) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (MainWindow != null)
                    {
                        if (MainWindow.WindowState == WindowState.Minimized)
                        {
                            MainWindow.WindowState = WindowState.Normal;
                        }
                        MainWindow.Activate();
                        MainWindow.Topmost = true;
                        MainWindow.Topmost = false;
                        MainWindow.Focus();
                    }
                });
            },
            null,
            -1,
            false);

        // Register Global Exception Handlers
        SetupGlobalExceptionHandling();

        try
        {
            var baseDir = Environment.GetEnvironmentVariable("DOCTORRX_DATA_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoctorRx");
            var logDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logDir);

            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: Path.Combine(logDir, "doctorrx-.log"),
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _host = Host.CreateDefaultBuilder(e.Args)
                .UseSerilog()
                .UseDefaultServiceProvider((context, options) =>
                {
#if DEBUG
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
#endif
                })
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
            var errorRef = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            Serilog.Log.Fatal(ex, "Fatal startup error [Ref: {ErrorRef}]", errorRef);

            MessageBox.Show(
                $"Failed to start DoctorRx workstation.\n\nError Reference ID: {errorRef}\nPlease quote this ID if contacting technical support.",
                "DoctorRx Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _registeredWait?.Unregister(null);
        _activateEvent?.Dispose();
        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
        }

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Serilog.Log.CloseAndFlush();
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
            if (args.Exception != null)
            {
                var errorRef = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                Serilog.Log.Warning(args.Exception, "Unobserved background task exception [Ref: {ErrorRef}]", errorRef);
            }
        };
    }

    private static void ShowSafeErrorDialog(string userFriendlyMessage, Exception ex)
    {
        var errorRef = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Serilog.Log.Error(ex, "Unhandled application error [Ref: {ErrorRef}]", errorRef);

        MessageBox.Show(
            $"{userFriendlyMessage}\n\nError Reference ID: {errorRef}\nPlease quote this ID if contacting technical support.",
            "DoctorRx System Notice",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
