using ImgTagFanOut.Models.CompareAlgorithms;

namespace ImgTagFanOut.Tests;

public class FileComparisonTests : IDisposable
{
    private readonly string _tempDir;

    public FileComparisonTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImgTagFanOut.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WriteFile(string name, int sizeInBytes, byte fill)
    {
        string path = Path.Combine(_tempDir, name);
        byte[] data = new byte[sizeInBytes];
        Array.Fill(data, fill);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public void FilesAreEqual_EqualFiles_ReturnsTrue()
    {
        string a = WriteFile("a.jpg", 1000, 0xAB);
        string b = WriteFile("b.jpg", 1000, 0xAB);

        Assert.True(FileExt.FilesAreEqual(new FileInfo(a), new FileInfo(b)));
    }

    [Fact]
    public void FilesAreEqual_DifferentFiles_ReturnsFalse()
    {
        string a = WriteFile("a.jpg", 1000, 0xAB);
        string b = WriteFile("b.jpg", 1000, 0xCD);

        Assert.False(FileExt.FilesAreEqual(new FileInfo(a), new FileInfo(b)));
    }

    [Fact]
    public void FilesAreEqual_DifferentLength_ReturnsFalse()
    {
        string a = WriteFile("a.jpg", 1000, 0xAB);
        string b = WriteFile("b.jpg", 999, 0xAB);

        Assert.False(FileExt.FilesAreEqual(new FileInfo(a), new FileInfo(b)));
    }

    [Fact]
    public void FilesAreEqual_SameFile_ReturnsTrue()
    {
        string a = WriteFile("a.jpg", 1000, 0xAB);

        Assert.True(FileExt.FilesAreEqual(new FileInfo(a), new FileInfo(a)));
    }

    [Fact]
    public void Compare_EqualFilesWithChunkSizeNotMultipleOf32_ReturnsTrue()
    {
        string a = WriteFile("a.jpg", 100, 0x42);
        string b = WriteFile("b.jpg", 100, 0x42);

        bool result = new ReadFileInChunksAndCompareAvx2(a, b, chunkSize: 100).Compare();

        Assert.True(result);
    }

    [Fact]
    public void Compare_DifferentFilesWithChunkSizeNotMultipleOf32_ReturnsFalse()
    {
        string a = WriteFile("a.jpg", 100, 0x42);
        string b = WriteFile("b.jpg", 100, 0x84);

        bool result = new ReadFileInChunksAndCompareAvx2(a, b, chunkSize: 100).Compare();

        Assert.False(result);
    }
}