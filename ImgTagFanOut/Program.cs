using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Reactive;
using ReactiveUI;
using Sentry;

namespace ImgTagFanOut;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

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
                    o.Debug = true;
                    
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
                RxApp.DefaultExceptionHandler = Observer.Create<Exception>(e =>
                {
                    SentrySdk.CaptureException(e);
                });
            })
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}