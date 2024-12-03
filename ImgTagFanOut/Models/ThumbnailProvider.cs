using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using ImageMagick;
using Serilog;

namespace ImgTagFanOut.Models;

public class ThumbnailProvider
{
    internal async Task<Bitmap?> GetThumbnail(string fullFilePath)
    {
        uint targetWidth = 400;
        try
        {
            return await GetThumbnailWithAvaloniaBitmap(fullFilePath, targetWidth);
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to fetch preview for: {fullFilePath} with Avalonia", e);
            try
            {
                return await GetThumbnailWithImageMagick(fullFilePath, targetWidth);
            }
            catch (Exception inner)
            {
                Log.Warning($"Unable to fetch preview for: {fullFilePath}", inner);
                return null;
            }
        }
    }

    private static async Task<Bitmap?> GetThumbnailWithAvaloniaBitmap(string fullFilePath, uint targetWidth)
    {
        await using FileStream fs = File.OpenRead(fullFilePath);

        // this should be the expected implemation
        return Bitmap.DecodeToWidth(fs, (int)targetWidth, BitmapInterpolationMode.MediumQuality);
    }

    private static async Task<Bitmap?> GetThumbnailWithImageMagick(string fullFilePath, uint targetWidth)
    {
        using MemoryStream ms = Program.RecyclableMemoryStreamManager.GetStream();

        MagickReadSettings settings = new()
        {
            // Set the width to the desired maximum width
            Width = targetWidth,
            // Use 0 for height to maintain the aspect ratio
            Height = 0,
            SyncImageWithExifProfile = false,
        };
        using (MagickImage magickImage = new(fullFilePath, settings))
        {
            await magickImage.WriteAsync(ms, MagickFormat.Bmp);
        }

        // Reset the position to the beginning of the stream
        ms.Seek(0, SeekOrigin.Begin);
        return new Bitmap(ms);
    }

    private static Bitmap ReduceSize(Bitmap fullImage, int targetWidth)
    {
        double newHeight = fullImage.Size.Width > targetWidth ? 400d / fullImage.Size.Width * fullImage.Size.Height : fullImage.Size.Height;

        Bitmap thumbnail = fullImage.CreateScaledBitmap(new PixelSize(targetWidth, (int)newHeight));

        return thumbnail;
    }
}
