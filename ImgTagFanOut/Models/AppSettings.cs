namespace ImgTagFanOut.Models;

public class AppSettings
{
    public string? LastFolder { get; set; }
    public bool? ErrorTrackingAllowed { get; set; }

    /// <summary>
    /// How published files are written. Defaults to full copies, which keeps the
    /// exported folder self-contained.
    /// </summary>
    public PublishLinkMode LinkMode { get; set; } = PublishLinkMode.Copy;
}
