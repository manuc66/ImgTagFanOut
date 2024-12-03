using System.IO;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public abstract class ReadIntoByteBufferInChunks : FileComparer
{
    protected readonly int ChunkSize;

    protected ReadIntoByteBufferInChunks(string filePath01, string filePath02, int chunkSize = 0)
        : base(filePath01, filePath02)
    {
        ChunkSize = chunkSize > 0 ? chunkSize : 4096 * 32;
    }

    protected ReadIntoByteBufferInChunks(FileInfo fileInfo01, FileInfo fileInfo02, int chunkSize = 0)
        : base(fileInfo01, fileInfo02)
    {
        ChunkSize = chunkSize > 0 ? chunkSize : 4096 * 32;
    }

    protected int ReadIntoBuffer(in Stream stream, in byte[] buffer)
    {
        int bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            // Reached end of stream.
            if (read == 0)
            {
                return bytesRead;
            }

            bytesRead += read;
        }

        return bytesRead;
    }
}
