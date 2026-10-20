using System;
using System.Runtime.InteropServices;

namespace ImgTagFanOut.Models;

/// <summary>
/// Creates filesystem hard links through the platform call the BCL does not
/// expose (there is no <c>File.CreateHardLink</c> in .NET 10).
/// </summary>
internal static partial class HardLink
{
    /// <summary>
    /// Links <paramref name="newFile"/> to <paramref name="existingFile"/>.
    /// </summary>
    /// <returns>
    /// False when the platform or the filesystem refuses the link — a different
    /// volume, a FAT/exFAT target, a network share — so the caller can fall back
    /// to a copy.
    /// </returns>
    public static bool TryCreate(string existingFile, string newFile)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return CreateHardLinkW(newFile, existingFile, IntPtr.Zero) != 0;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return Link(existingFile, newFile) == 0;
            }

            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Link(string oldPath, string newPath);
}
