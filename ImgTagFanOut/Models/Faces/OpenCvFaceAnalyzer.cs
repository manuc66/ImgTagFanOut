using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using OpenCvSharp;
using Serilog;

namespace ImgTagFanOut.Models.Faces;

/// <summary>
/// YuNet detection and SFace embeddings through OpenCV.
/// <para>
/// Returned coordinates are <b>normalized</b> (0..1): they stay valid whatever
/// size the image is decoded at later, and the UI only has to multiply by the
/// size of the control.
/// </para>
/// <para>
/// This class is not thread-safe (the underlying OpenCV objects are not):
/// create one instance per worker thread. Loading the models costs a few tens
/// of milliseconds, so it must not happen per image.
/// </para>
/// </summary>
public sealed class OpenCvFaceAnalyzer : IFaceAnalyzer, IDisposable
{
    public const string DetectorModelFileName = "face_detection_yunet_2023mar.onnx";
    public const string EmbedderModelFileName = "face_recognition_sface_2021dec.onnx";

    private const int DefaultWorkingLongEdge = 1280;
    private const float DefaultScoreThreshold = 0.6f;
    private const float DefaultNmsThreshold = 0.3f;

    /// <summary>
    /// Beyond this, the image is downscaled before analysis: a 112 px crop does
    /// not need full resolution, and it bounds the memory used per image.
    /// </summary>
    private const int MaxDecodedLongEdge = 2000;

    private const int FaceRowLength = 15;

    private readonly FaceDetectorYN _detector;
    private readonly FaceRecognizerSF _recognizer;
    private readonly int _workingLongEdge;
    private readonly string _detectorVersion;

    public OpenCvFaceAnalyzer(
        string modelsDirectory,
        int workingLongEdge = DefaultWorkingLongEdge,
        float scoreThreshold = DefaultScoreThreshold,
        float nmsThreshold = DefaultNmsThreshold)
    {
        _workingLongEdge = workingLongEdge;

        string detectorModel = RequireModel(modelsDirectory, DetectorModelFileName);
        string embedderModel = RequireModel(modelsDirectory, EmbedderModelFileName);

        _detector = FaceDetectorYN.Create(
            detectorModel,
            "",
            new Size(320, 320),
            scoreThreshold,
            nmsThreshold,
            5000);

        _recognizer = FaceRecognizerSF.Create(embedderModel, "");

        // Invariant formatting: the current culture must not change the cache key.
        _detectorVersion = string.Create(
            CultureInfo.InvariantCulture,
            $"yunet-2023mar/w{workingLongEdge}/s{scoreThreshold:0.00}/n{nmsThreshold:0.00}");
    }

    /// <summary>Models directory, next to the executable.</summary>
    public static string DefaultModelsDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Models");

    public string DetectorVersion => _detectorVersion;

    public string EmbedderVersion => "sface-2021dec/rgb255/l2";

    public Task<FaceAnalysisResult> AnalyzeAsync(
        string fullFilePath,
        IReadOnlyList<FaceDetection>? knownDetections,
        bool computeEmbeddings,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Analyze(fullFilePath, knownDetections, computeEmbeddings, cancellationToken), cancellationToken);
    }

    private FaceAnalysisResult Analyze(
        string fullFilePath,
        IReadOnlyList<FaceDetection>? knownDetections,
        bool computeEmbeddings,
        CancellationToken cancellationToken)
    {
        using Mat image = LoadImage(fullFilePath);

        IReadOnlyList<FaceDetection> detections = knownDetections ?? Detect(image);

        List<AnalyzedFace> faces = new(detections.Count);
        foreach (FaceDetection detection in detections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? embedding = computeEmbeddings ? Embed(image, detection) : null;
            faces.Add(new AnalyzedFace(detection, embedding));
        }

        return new FaceAnalysisResult(faces);
    }

    private static string RequireModel(string modelsDirectory, string fileName)
    {
        string path = Path.Combine(modelsDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Face model not found: {path}. It should ship with the application.",
                path);
        }

        return path;
    }

    /// <summary>
    /// Loads the image as BGR. OpenCV decodes neither HEIC nor RAW, so those fall
    /// back to Magick.NET, already used for thumbnails.
    /// </summary>
    internal static Mat LoadImage(string fullFilePath)
    {
        Mat? image = Cv2.ImRead(fullFilePath, ImreadModes.Color);
        if (image is not null && !image.Empty())
        {
            return LimitSize(image);
        }

        image?.Dispose();

        Log.Debug("Format not decoded by OpenCV, trying Magick.NET: {File}", fullFilePath);
        return LimitSize(LoadWithImageMagick(fullFilePath));
    }

    private static Mat LoadWithImageMagick(string fullFilePath)
    {
        MagickReadSettings settings = new()
        {
            // The decoder downscales while reading: a 50 MP RAW never lands in memory.
            Width = MaxDecodedLongEdge,
            Height = MaxDecodedLongEdge,
        };

        using MagickImage magick = new(fullFilePath, settings);
        magick.AutoOrient();

        byte[] bmp = magick.ToByteArray(MagickFormat.Bmp);
        Mat decoded = Cv2.ImDecode(bmp, ImreadModes.Color);
        if (decoded.Empty())
        {
            decoded.Dispose();
            throw new InvalidOperationException($"Cannot decode image: {fullFilePath}");
        }

        return decoded;
    }

    private static Mat LimitSize(Mat image)
    {
        int longEdge = Math.Max(image.Width, image.Height);
        if (longEdge <= MaxDecodedLongEdge)
        {
            return image;
        }

        double scale = MaxDecodedLongEdge / (double)longEdge;
        Mat resized = new();
        Cv2.Resize(
            image,
            resized,
            new Size((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale)),
            interpolation: InterpolationFlags.Area);
        image.Dispose();
        return resized;
    }

    private IReadOnlyList<FaceDetection> Detect(Mat image)
    {
        int longEdge = Math.Max(image.Width, image.Height);
        double scale = longEdge > _workingLongEdge ? _workingLongEdge / (double)longEdge : 1.0;

        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(image.Height * scale));

        using Mat working = new();
        if (scale < 1.0)
        {
            Cv2.Resize(image, working, new Size(width, height), interpolation: InterpolationFlags.Area);
        }
        else
        {
            image.CopyTo(working);
        }

        _detector.SetInputSize(new Size(width, height));

        using Mat faces = new();
        _detector.Detect(working, faces);

        if (faces.Empty())
        {
            return [];
        }

        if (!faces.GetArray(out float[] values))
        {
            return [];
        }

        int count = values.Length / FaceRowLength;
        List<FaceDetection> detections = new(count);
        for (int i = 0; i < count; i++)
        {
            int offset = i * FaceRowLength;
            float[] landmarks = new float[FaceLandmarks.ValueCount];
            Array.Copy(values, offset + 4, landmarks, 0, FaceLandmarks.ValueCount);

            detections.Add(new FaceDetection(
                X: values[offset] / width,
                Y: values[offset + 1] / height,
                Width: values[offset + 2] / width,
                Height: values[offset + 3] / height,
                Score: values[offset + FaceRowLength - 1],
                Landmarks: NormalizeLandmarks(landmarks, width, height)));
        }

        // Stable order across runs: the position is used as a display identifier.
        return detections.OrderBy(d => d.X).ToArray();
    }

    private static float[] NormalizeLandmarks(float[] landmarks, int width, int height)
    {
        float[] normalized = new float[landmarks.Length];
        for (int i = 0; i < landmarks.Length; i += 2)
        {
            normalized[i] = landmarks[i] / width;
            normalized[i + 1] = landmarks[i + 1] / height;
        }

        return normalized;
    }

    private byte[] Embed(Mat image, FaceDetection detection)
    {
        using Mat faceRow = BuildFaceRow(image, detection);

        using Mat aligned = new();
        _recognizer.AlignCrop(image, faceRow, aligned);

        using Mat feature = new();
        _recognizer.Feature(aligned, feature);

        if (!feature.GetArray(out float[] values) || values.Length == 0)
        {
            throw new InvalidOperationException("Empty embedding: the SFace model produced nothing.");
        }

        return NormalizeToBytes(values);
    }

    private static Mat BuildFaceRow(Mat image, FaceDetection detection)
    {
        float width = image.Width;
        float height = image.Height;

        float[] values = new float[FaceRowLength];
        values[0] = detection.X * width;
        values[1] = detection.Y * height;
        values[2] = detection.Width * width;
        values[3] = detection.Height * height;

        for (int i = 0; i < FaceLandmarks.ValueCount; i += 2)
        {
            values[4 + i] = detection.Landmarks[i] * width;
            values[5 + i] = detection.Landmarks[i + 1] * height;
        }

        values[FaceRowLength - 1] = detection.Score;

        Mat faceRow = new(1, FaceRowLength, MatType.CV_32F);
        faceRow.SetArray(values);
        return faceRow;
    }

    /// <summary>L2-normalizes then serializes as little-endian float32.</summary>
    private static byte[] NormalizeToBytes(float[] values)
    {
        double sum = 0;
        foreach (float value in values)
        {
            sum += (double)value * value;
        }

        double norm = Math.Sqrt(sum);
        if (norm < 1e-9)
        {
            throw new InvalidOperationException("Degenerate embedding (zero norm).");
        }

        byte[] bytes = new byte[values.Length * sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(float)), (float)(values[i] / norm));
        }

        return bytes;
    }

    public void Dispose()
    {
        _detector.Dispose();
        _recognizer.Dispose();
    }
}
