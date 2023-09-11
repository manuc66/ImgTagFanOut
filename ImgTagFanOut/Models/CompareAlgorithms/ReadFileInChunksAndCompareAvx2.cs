using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public class ReadFileInChunksAndCompareAvx2 : ReadIntoByteBufferInChunks
{
    public ReadFileInChunksAndCompareAvx2(string filePath01, string filePath02, int chunkSize = 0)
        : base(filePath01, filePath02, chunkSize)
    {
    }

    public ReadFileInChunksAndCompareAvx2(FileInfo fileInfo01, FileInfo fileInfo02, int chunkSize = 0)
        : base(fileInfo01, fileInfo02, chunkSize)
    {
    }

    protected override bool OnCompare()
    {
        using FileStream fileStream = FileInfo1.OpenRead();
        using FileStream openRead = FileInfo2.OpenRead();
        return StreamAreEqual(fileStream, openRead);
    }

    private unsafe bool StreamAreEqual(in Stream stream1, in Stream stream2)
    {
        byte[] buffer1 = new byte[ChunkSize];
        byte[] buffer2 = new byte[ChunkSize];

        while (true)
        {
            int count1 = ReadIntoBuffer(stream1, buffer1);
            int count2 = ReadIntoBuffer(stream2, buffer2);

            if (count1 != count2)
            {
                return false;
            }

            if (count1 == 0)
            {
                return true;
            }

            fixed (byte* oh1 = buffer1)
            {
                fixed (byte* oh2 = buffer2)
                {
                    int totalProcessed = 0;
                    while (totalProcessed < count1)
                    {
                        Vector256<byte> result = Avx2.CompareEqual(Avx.LoadVector256(oh1 + totalProcessed), Avx.LoadVector256(oh2 + totalProcessed));
                        if (Avx2.MoveMask(result) != -1)
                        {
                            return false;
                        }
                        totalProcessed += Vector256<byte>.Count;
                    }
                }
            }
        }
    }
}