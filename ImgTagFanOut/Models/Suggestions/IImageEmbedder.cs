using System;

namespace ImgTagFanOut.Models.Suggestions;

/// <summary>Turns a photo into a normalised vector used to compare it with tagged photos.</summary>
public interface IImageEmbedder : IDisposable
{
    /// <summary>Changes whenever the model or the preprocessing changes, invalidating cached vectors.</summary>
    string Version { get; }

    /// <summary>Runs on the caller's thread; one call decodes the image once.</summary>
    float[] Embed(string fullPath);
}
