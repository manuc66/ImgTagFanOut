namespace ImgTagFanOut.Models;

/// <summary>What publishing one file did.</summary>
/// <param name="Source">Full path of the file in the library.</param>
/// <param name="Destination">Target path, or null when the source file is missing.</param>
/// <param name="Created">True when this run wrote a copy or a link.</param>
/// <param name="Linked">True when the file was hard-linked instead of copied.</param>
public readonly record struct PublishFileResult(string Source, string? Destination, bool Created, bool Linked);
