using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Logging;
using ReactiveUI;
using Sentry;
using Serilog;
using Splat;
using ILogger = Serilog.ILogger;

namespace ImgTagFanOut;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppBootstrapper.Register(Locator.CurrentMutable, Locator.Current);
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
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
                SentrySdk.Init(o =>
                {
                    // Tells which project in Sentry to send events to:
                    o.Dsn = "https://2fd61307fdf9b3a63804db34c9bc51eb@o4505868956860416.ingest.sentry.io/4505869112639488";
                   
                    // When configuring for the first time, to see what the SDK is doing:
                    //o.Debug = true;
                    
                    // This option is recommended. It enables Sentry's "Release Health" feature.
                    o.AutoSessionTracking = true;
                    
                    // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    o.TracesSampleRate = 1.0;

                    //o.Debug = true;
                    
                    //o.DiagnosticLevel = SentryLevel.Debug;
                });
                AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                };
                RxApp.DefaultExceptionHandler = Observer.Create<Exception>(e =>
                {
                    SentrySdk.CaptureException(e);
                });
                
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Sentry(o =>
                    {
                        // Debug and higher are stored as breadcrumbs (default is Information)
                        o.MinimumBreadcrumbLevel = Serilog.Events.LogEventLevel.Debug;
                        // Warning and higher is sent as event (default is Error)
                        o.MinimumEventLevel = Serilog.Events.LogEventLevel.Warning;
                    })
                    .CreateLogger();

                Logger.Sink = new SerilogAvaloniaSink(Log.Logger);
                
                Log.Logger.Error("Hello");
            })
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
public class SerilogAvaloniaSink : ILogSink
{
    private readonly ILogger _logger;

    public SerilogAvaloniaSink(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogEventLevel level, string area)
    {
        Serilog.Events.LogEventLevel logLevel = GetSerilogLogLevel(level, area);
        
        // Except with binding errors, ignore anything that is information or lower
        return (area == "Binding" || logLevel > Serilog.Events.LogEventLevel.Information) && _logger.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        Serilog.Events.LogEventLevel logLevel = GetSerilogLogLevel(level, area);

        ILogger logger = source != null ? _logger.ForContext(source.GetType()) : _logger;
        logger.Write(logLevel, messageTemplate);
    }

    /// <inheritdoc />
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        Serilog.Events.LogEventLevel logLevel = GetSerilogLogLevel(level, area);

        ILogger logger = source != null ? _logger.ForContext(source.GetType()) : _logger;
        logger.Write(logLevel, messageTemplate, propertyValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Serilog.Events.LogEventLevel GetSerilogLogLevel(LogEventLevel level, string area)
    {
        // Avalonia considers binding errors warnings, we'll treat them Verbose as to not spam people's logs
        // And yes we should fix them instead but we can't always: https://github.com/AvaloniaUI/Avalonia/issues/5762
        if (area == "Binding")
            return Serilog.Events.LogEventLevel.Verbose;

        switch (level)
        {
            case LogEventLevel.Verbose:
                return Serilog.Events.LogEventLevel.Verbose;
            case LogEventLevel.Debug:
                return Serilog.Events.LogEventLevel.Debug;
            case LogEventLevel.Information:
                return Serilog.Events.LogEventLevel.Information;
            case LogEventLevel.Warning:
                return Serilog.Events.LogEventLevel.Warning;
            case LogEventLevel.Error:
                return Serilog.Events.LogEventLevel.Error;
            case LogEventLevel.Fatal:
                return Serilog.Events.LogEventLevel.Fatal;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }
}