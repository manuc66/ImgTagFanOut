using System.Text.Json.Serialization;

namespace ImgTagFanOut.Models;

/// <summary>
/// How a published file reaches the target folder.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PublishLinkMode>))]
public enum PublishLinkMode
{
    /// <summary>
    /// Write a full copy, so the published folder is self-contained and can be
    /// moved, zipped or handed to someone else.
    /// </summary>
    Copy,

    /// <summary>
    /// Hard-link the file when the source and the target are on the same volume,
    /// and fall back to a copy otherwise. Saves the space of a second copy; the
    /// two paths share their content, so editing one side edits the other.
    /// </summary>
    HardLinkWhenPossible,
}
