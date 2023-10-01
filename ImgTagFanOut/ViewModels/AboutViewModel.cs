using System.Diagnostics;

namespace ImgTagFanOut.ViewModels;

public class AboutViewModel : ViewModelBase
{

    public string? Version { get; }
    public string? Copyright { get; }


    public AboutViewModel()
    {
        var assemblyLocation = Process.GetCurrentProcess().MainModule?.FileName;
        if (assemblyLocation == null) return;

        var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation);
        Version = fileVersionInfo.ProductVersion;
        Copyright = fileVersionInfo.LegalCopyright;
        //Console.WriteLine(fileVersionInfo.CompanyName); // Company
        //Console.WriteLine(fileVersionInfo.Comments); // Description
    }
}