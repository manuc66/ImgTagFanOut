using System.IO;
using System.Runtime.Intrinsics.X86;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public static class FileExt
{
    public static bool FilesAreEqual(FileInfo first, FileInfo second)
    {
        if (Avx2.IsSupported)
        {
            return new ReadFileInChunksAndCompareAvx2(first, second).Compare();
        }

        return new ReadFileInChunksAndCompare(first, second).Compare();
    }
}
