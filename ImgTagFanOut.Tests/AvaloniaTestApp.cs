using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using ImgTagFanOut;

[assembly: AvaloniaTestApplication(typeof(ImgTagFanOut.Tests.TestAppBuilder))]

// The headless Avalonia platform owns a single dispatcher and a single render
// loop. Running test classes concurrently makes two threads initialise and
// tear it down at the same time, which surfaces as random "The calling thread
// cannot access this object" cleanup failures. Serialise the whole assembly:
// the suite takes a few seconds, flakiness costs far more.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ImgTagFanOut.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}