using ImgTagFanOut.Models.Suggestions;

namespace ImgTagFanOut.Tests;

public class OnnxImageEmbedderTests
{
    private static string DemoPhoto(string person, string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "demo", "faces", person, file);
    }

    private static double Cosine(float[] left, float[] right)
    {
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (int i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    [Fact]
    public void Model_ProducesANormalisedDescriptor()
    {
        using OnnxImageEmbedder embedder = new();

        float[] vector = embedder.Embed(DemoPhoto("obama", "photo-1.jpg"));

        Assert.Equal(1280, vector.Length);
        Assert.All(vector, value => Assert.True(float.IsFinite(value)));
        Assert.Equal(1, Math.Sqrt(vector.Sum(value => value * value)), 3);
    }

    [Fact]
    public void SameImage_IsStable()
    {
        using OnnxImageEmbedder embedder = new();

        float[] first = embedder.Embed(DemoPhoto("obama", "photo-1.jpg"));
        float[] second = embedder.Embed(DemoPhoto("obama", "photo-1.jpg"));

        Assert.Equal(1, Cosine(first, second), 4);
    }

    [Fact]
    public void SamePerson_IsCloserThanAnotherPerson()
    {
        using OnnxImageEmbedder embedder = new();

        float[] obama1 = embedder.Embed(DemoPhoto("obama", "photo-1.jpg"));
        float[] obama2 = embedder.Embed(DemoPhoto("obama", "photo-2.jpg"));
        float[] reagan = embedder.Embed(DemoPhoto("reagan", "photo-1.jpg"));

        Assert.True(
            Cosine(obama1, obama2) > Cosine(obama1, reagan),
            $"same person {Cosine(obama1, obama2):F3} should beat another person {Cosine(obama1, reagan):F3}");
    }

    [Fact]
    public void Model_AcceptsPngWithAlpha()
    {
        string path = Path.Combine(Path.GetTempPath(), "ImgTagFanOut-embed-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, FakeImageGenerator.GeneratePng(64, 48, 10, 20, 30));

        try
        {
            using OnnxImageEmbedder embedder = new();
            float[] vector = embedder.Embed(path);

            Assert.Equal(1280, vector.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingModel_FailsWithAClearError()
    {
        Assert.Throws<FileNotFoundException>(() => new OnnxImageEmbedder(Path.Combine(Path.GetTempPath(), "missing.onnx")));
    }
}
