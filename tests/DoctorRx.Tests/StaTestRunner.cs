using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DoctorRx.Presentation.Converters;

namespace DoctorRx.Tests;

/// <summary>
/// Lightweight in-repo STA thread helper for executing WPF UI test assertions without external packages.
/// </summary>
public static class StaTestRunner
{
    private static readonly object SyncLock = new();

    public static void Run(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationContext();
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public static void EnsureApplicationContext()
    {
        lock (SyncLock)
        {
            if (System.Windows.Application.Current == null)
            {
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/DoctorRx.Presentation;component/Theme/Colors.xaml", UriKind.RelativeOrAbsolute)
                });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/DoctorRx.Presentation;component/Theme/Icons.xaml", UriKind.RelativeOrAbsolute)
                });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/DoctorRx.Presentation;component/Theme/Styles.xaml", UriKind.RelativeOrAbsolute)
                });

                app.Resources.Add("BoolToVisConverter", new BooleanToVisibilityConverter());
                app.Resources.Add("InverseBoolToVisConverter", new InverseBoolToVisibilityConverter());
                app.Resources.Add("FollowUpModeDisplayConverter", new FollowUpModeDisplayConverter());
            }
        }
    }
}
