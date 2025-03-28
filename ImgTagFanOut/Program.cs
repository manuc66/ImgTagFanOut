using System;
using System.Diagnostics;
using System.Reactive;
using Avalonia;
using Avalonia.ReactiveUI;
using ImgTagFanOut.Models;
using Microsoft.IO;
using ReactiveUI;
using Sentry;
using Serilog;

namespace ImgTagFanOut;

internal static class Program
{
    private static IDisposable? _errorTracking;
    internal static IDisposable? ErrorTracking => _errorTracking;
    internal static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new RecyclableMemoryStreamManager();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureSentryAndLogging();

        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(e => { HandleException(e, "Unhandled exception"); });
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex, $"Unhandled exception in domain: {AppDomain.CurrentDomain.FriendlyName}");
            }

            CleanupResources();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupResources();


        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            HandleException(e, "Critical application error during startup.");
        }
        finally
        {
            CleanupResources();
        }
    }


    private static void HandleException(Exception e, string context)
    {
        if (Debugger.IsAttached)
            Debugger.Break();

        try
        {
            Log.Error(e, context);
            SentrySdk.CaptureException(e);
        }
        catch
        {
            // Fallback error handling
            Console.WriteLine($@"Critical error: {context}{Environment.NewLine}{e}");
        }
    }


    private static void CleanupResources()
    {
        ErrorTracking?.Dispose();
        Log.CloseAndFlush();
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(
                new X11PlatformOptions
                {
                    UseDBusFilePicker = false, // to disable FreeDesktop file picker
                }
            )
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();


    private static void ConfigureSentryAndLogging()
    {
        Settings settings = new();
        AppSettings readSettings = settings.ReadSettings();
        LoggerConfiguration loggerConfiguration = new LoggerConfiguration();
        loggerConfiguration
            .Enrich.WithProperty("ApplicationName", "ImgTagFanOut")
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("ApplicationVersion", GetApplicationVersion())
            .Enrich.WithProperty("OS", Environment.OSVersion.ToString())
            .Enrich.WithProperty("DOTNET_ENVIRONMENT", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");

        loggerConfiguration.WriteTo.File(EnvironmentService.GetLogFile());

        if (readSettings.ErrorTrackingAllowed ?? true)
        {
            InitializeSentry(loggerConfiguration);
        }

        Log.Logger = loggerConfiguration.CreateLogger();
    }
    private static string GetApplicationVersion()
    {
        return typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown";
    }


    private static void InitializeSentry(LoggerConfiguration loggerConfiguration)
    {
        string sentryDsn = "https://2fd61307fdf9b3a63804db34c9bc51eb@o4505868956860416.ingest.sentry.io/4505869112639488";
        loggerConfiguration.WriteTo.Sentry(o => o.Dsn = sentryDsn);

        _errorTracking = SentrySdk.Init(o =>
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
    }
}