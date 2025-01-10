using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Blake3;

namespace ImgTagFanOut.ViewModels;

public class HashEvaluator
{

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken ctsToken)
    {
        string hash;
        using Hasher hasher = Hasher.New();
        await using FileStream fs = File.OpenRead(filePath);
        ArrayPool<byte> sharedArrayPool = ArrayPool<byte>.Shared;
        byte[] buffer = sharedArrayPool.Rent(131072);
        Array.Fill<byte>(buffer, 0);
        try
        {
            for (int read; (read = await fs.ReadAsync(buffer, ctsToken)) != 0; )
            {
                hasher.Update(buffer.AsSpan(start: 0, read));
            }

            hash = hasher.Finalize().ToString();
        }
        finally
        {
            sharedArrayPool.Return(buffer);
        }

        return hash;
    }
}