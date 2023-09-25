

// This code written by Daniel Peñalba and released at  https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows/21752100#21752100
// Absolutely amazing and easy to use class to use the IShellItemImageFactory Windows interface provided in Windows 7 and upwards.
// Actually reliably gets 256x256 icons from files!


namespace DisplayMagician
{
    [Flags]
    public enum ThumbnailOptions
    {
        None = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
        CropToSquare = 0x20,
        WideThumbnails = 0x40,
        IconBackground = 0x80,
        ScaleUp = 0x100
    }
}