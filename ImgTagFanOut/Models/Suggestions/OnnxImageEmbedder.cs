using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImgTagFanOut.Models.Suggestions;

/// <summary>
/// MobileNetV2 image descriptor, executed locally by ONNX Runtime. The
/// classifier head is ignored: the 1280-value pooled features are a far better
/// similarity signal than the ImageNet class scores.
/// </summary>
/// <remarks>
/// The model is 14 MB and runs in tens of milliseconds on a CPU. It stays on
/// this machine: no photo, path or score leaves the library folder.
/// </remarks>
public sealed class OnnxImageEmbedder : IImageEmbedder
{
    public const string ModelFileName = "image_embedding_mobilenetv2.onnx";

    /// <summary>Bump when the preprocessing or the output tensor changes.</summary>
    public const string CurrentVersion = "mobilenetv2-1.0-224/features/l2/v1";

    private const int InputSize = 224;
    private const int ResizeShortestEdge = 256;

    public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", ModelFileName);

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    public OnnxImageEmbedder()
        : this(DefaultModelPath) { }

    public OnnxImageEmbedder(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The local image embedding model is missing.", modelPath);
        }

        using SessionOptions options = new()
        {
            InterOpNumThreads = 1,
            // Keep a photo book responsive while a scan runs in the background.
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount, 1, 4),
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _session = new InferenceSession(modelPath, options);

        _inputName = _session.InputMetadata.Keys.First();

        // The exported model has two outputs: the ImageNet logits and the
        // pooled features. Pick the features by their known width instead of
        // relying on a generated node name.
        _outputName = _session.OutputMetadata
            .FirstOrDefault(output => output.Value.Dimensions.Contains(1280))
            .Key ?? _session.OutputMetadata.Keys.First();
    }

    public string Version => CurrentVersion;

    public float[] Embed(string fullPath)
    {
        float[] tensor = Preprocess(fullPath);

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(
            [NamedOnnxValue.CreateFromTensor(_inputName, new DenseTensor<float>(tensor, [1, 3, InputSize, InputSize]))]);

        DisposableNamedOnnxValue features = results.First(result => result.Name == _outputName);
        float[] vector = features.AsEnumerable<float>().ToArray();

        double norm = 0;
        foreach (float value in vector)
        {
            norm += value * value;
        }

        norm = Math.Sqrt(norm);
        if (norm <= 0)
        {
            return vector;
        }

        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private static float[] Preprocess(string fullPath)
    {
        using MagickImage image = new(fullPath);
        image.AutoOrient();

        // Resize the shortest edge to 256, then center-crop 224x224, as the
        // model's preprocessor_config.json describes.
        if (image.Width <= image.Height)
        {
            image.Resize(new MagickGeometry(ResizeShortestEdge, 0));
        }
        else
        {
            image.Resize(new MagickGeometry(0, ResizeShortestEdge));
        }

        image.Crop(InputSize, InputSize, Gravity.Center);
        image.ResetPage();
        image.Depth = 8;
        image.Format = MagickFormat.Rgb;

        using MemoryStream stream = new();
        image.Write(stream);
        byte[] rgb = stream.ToArray();

        if (rgb.Length != InputSize * InputSize * 3)
        {
            throw new InvalidDataException($"Unexpected RGB buffer length {rgb.Length} for {fullPath}.");
        }

        // (value / 255 - 0.5) / 0.5, in NCHW order.
        float[] tensor = new float[3 * InputSize * InputSize];
        int plane = InputSize * InputSize;
        for (int y = 0; y < InputSize; y++)
        {
            for (int x = 0; x < InputSize; x++)
            {
                int source = (y * InputSize + x) * 3;
                for (int channel = 0; channel < 3; channel++)
                {
                    tensor[channel * plane + y * InputSize + x] = rgb[source + channel] / 127.5f - 1f;
                }
            }
        }

        return tensor;
    }
}
