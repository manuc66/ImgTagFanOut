namespace ImgTagFanOut.Models;

/// <summary>What publishing one file did.</summary>
/// <param name="Source">Full path of the file in the library.</param>
/// <param name="Destination">Target path, or null when the source file is missing.</param>
/// <param name="Created">True when this run wrote a copy or a link.</param>
/// <param name="Linked">True when the file was hard-linked instead of copied.</param>
/// <param name="Error">Why the file could not be published, or null on success.</param>
public readonly record struct PublishFileResult(
    string Source,
    string? Destination,
    bool Created,
    bool Linked,
    string? Error = null)
{
    /// <summary>True when this file was skipped or failed rather than published.</summary>
    public bool Failed => Error is not null;
}
