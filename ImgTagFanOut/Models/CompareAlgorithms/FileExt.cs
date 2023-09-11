using System.IO;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public class FileExt
{
    public static bool FilesAreEqual(FileInfo first, FileInfo second)
    {
        return new
            // ReadFileInChunksAndCompareVector      
            ReadFileInChunksAndCompareAvx2
            (first, second).Compare();
    }
}