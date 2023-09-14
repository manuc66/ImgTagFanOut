using Avalonia;
using Microsoft.Extensions.Configuration;
using Serilog;
using Splat;
using Splat.Serilog;

namespace ImgTagFanOut;

public class AppBootstrapper
{
    public AppBootstrapper(Application app)
    {
    }

    public static void Register(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver)
    {

services.UseSerilogFullLogger();
//services.UseSerilogFullLogger();

    }
}