using SkiaSharp;

namespace ImgTagFanOut.Tests;

public static class FakeImageGenerator
{
    public static byte[] GeneratePng(int width = 32, int height = 32, byte red = 255, byte green = 0, byte blue = 0)
    {
        using SKBitmap bitmap = new(width, height);
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(new SKColor(red, green, blue));
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static string WritePng(string directory, string fileName, int width = 32, int height = 32, byte red = 255, byte green = 0, byte blue = 0)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, GeneratePng(width, height, red, green, blue));
        return path;
    }
}