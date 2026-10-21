using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImgTagFanOut.ViewModels;

/// <summary>
/// Cuts a face out of a photo using its normalized box. Shared by the face strip
/// and the unknown-face clusters so both crop the same way.
/// </summary>
public static class FaceCrop
{
    /// <summary>A little margin keeps the crop readable.</summary>
    private const double Margin = 0.15;

    public static IImage? Create(Bitmap? source, float x, float y, float width, float height)
    {
        if (source is null || source.PixelSize.Width == 0)
        {
            return null;
        }

        double sourceWidth = source.PixelSize.Width;
        double sourceHeight = source.PixelSize.Height;

        double margin = Margin * Math.Max(width, height);
        int left = (int)Math.Max(0, (x - margin) * sourceWidth);
        int top = (int)Math.Max(0, (y - margin) * sourceHeight);
        int cropWidth = (int)Math.Min(sourceWidth - left, (width + 2 * margin) * sourceWidth);
        int cropHeight = (int)Math.Min(sourceHeight - top, (height + 2 * margin) * sourceHeight);

        if (cropWidth <= 1 || cropHeight <= 1)
        {
            return null;
        }

        return new CroppedBitmap(source, new PixelRect(left, top, cropWidth, cropHeight));
    }
}
