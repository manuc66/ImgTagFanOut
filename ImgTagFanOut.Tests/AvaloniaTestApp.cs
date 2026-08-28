using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using ImgTagFanOut;

[assembly: AvaloniaTestApplication(typeof(ImgTagFanOut.Tests.TestAppBuilder))]

namespace ImgTagFanOut.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}