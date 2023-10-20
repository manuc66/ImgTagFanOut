﻿using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Reactive;
using ImgTagFanOut.Models;
using ReactiveUI;
using Sentry;
using Serilog;

namespace ImgTagFanOut;

class Program
{
    internal static IDisposable? ErrorTracking;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        Log.CloseAndFlush();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                UseDBusFilePicker = false // to disable FreeDesktop file picker
            })
            .AfterSetup(_ =>
            {
                Settings settings = new();
                AppSettings readSettings = settings.ReadSettings();
                LoggerConfiguration loggerConfiguration = new LoggerConfiguration();
                loggerConfiguration.WriteTo.File(EnvironmentService.GetLogFile());
                if (readSettings.ErrorTrackingAllowed ?? true)
                {
                    string sentryDsn = "https://2fd61307fdf9b3a63804db34c9bc51eb@o4505868956860416.ingest.sentry.io/4505869112639488";
                   loggerConfiguration
                        .WriteTo.Sentry(o => o.Dsn =sentryDsn);
                    
                    ErrorTracking = SentrySdk.Init(o =>
                    {
                        // Tells which project in Sentry to send events to:
                        o.Dsn = sentryDsn;

                        // When configuring for the first time, to see what the SDK is doing:
                        //o.Debug = true;

                        // This option is recommended. It enables Sentry's "Release Health" feature.
                        o.AutoSessionTracking = true;

                        // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
                        // We recommend adjusting this value in production.
                        o.TracesSampleRate = 1.0;
                    });
                    AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
                    {
                        if (e.ExceptionObject is Exception ex)
                        {
                            SentrySdk.CaptureException(ex);
                        }
                    };
                    RxApp.DefaultExceptionHandler = Observer.Create<Exception>(e => { SentrySdk.CaptureException(e); });
                }
                Log.Logger = loggerConfiguration
                    .CreateLogger();
            })
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}