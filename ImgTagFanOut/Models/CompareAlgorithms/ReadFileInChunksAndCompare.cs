using System.IO;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public class ReadFileInChunksAndCompare : ReadIntoByteBufferInChunks
{
    public ReadFileInChunksAndCompare(string filePath01, string filePath02, int chunkSize = 0)
        : base(filePath01, filePath02, chunkSize) { }

    public ReadFileInChunksAndCompare(FileInfo fileInfo01, FileInfo fileInfo02, int chunkSize = 0)
        : base(fileInfo01, fileInfo02, chunkSize) { }

    protected override bool OnCompare()
    {
        using FileStream fileStream = FileInfo1.OpenRead();
        using FileStream openRead = FileInfo2.OpenRead();
        return StreamAreEqual(fileStream, openRead);
    }

    private bool StreamAreEqual(in Stream stream1, in Stream stream2)
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

            for (int i = 0; i < count1; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    return false;
                }
            }
        }
    }
}