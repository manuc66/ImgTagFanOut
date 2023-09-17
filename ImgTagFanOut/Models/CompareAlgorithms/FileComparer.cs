using System;
using System.IO;

namespace ImgTagFanOut.Models.CompareAlgorithms;

public abstract class FileComparer
{
    /// <summary>
    /// Fileinfo for source file
    /// </summary>
    protected readonly FileInfo FileInfo1;

    /// <summary>
    /// Fileinfo for target file
    /// </summary>
    protected readonly FileInfo FileInfo2;

    /// <summary>
    /// Base class for creating a file comparer
    /// </summary>
    /// <param name="filePath01">Absolute path to source file</param>
    /// <param name="filePath02">Absolute path to target file</param>
    protected FileComparer(string filePath01, string filePath02) : this(new(filePath01),
        new FileInfo(filePath02))
    {
    }

    protected FileComparer(FileInfo fileInfo01, FileInfo fileInfo02)
    {
        FileInfo1 = fileInfo01 ?? throw new ArgumentNullException(nameof(fileInfo01));
        FileInfo2 = fileInfo02 ?? throw new ArgumentNullException(nameof(fileInfo02));
        if (FileInfo1.Exists)
        {
            throw new FileNotFoundException(fileInfo01.FullName);
        }

        if (FileInfo2.Exists)
        {
            throw new FileNotFoundException(fileInfo02.FullName);
        }
    }


    /// <summary>
    /// Compares the two given files and returns true if the files are the same
    /// </summary>
    /// <returns>true if the files are the same, false otherwise</returns>
    public bool Compare()
    {
        if (IsDifferentLength())
        {
            return false;
        }

        if (IsSameFile())
        {
            return true;
        }

        return OnCompare();
    }

    /// <summary>
    /// Compares the two given files and returns true if the files are the same
    /// </summary>
    /// <returns>true if the files are the same, false otherwise</returns>
    protected abstract bool OnCompare();

    private bool IsSameFile()
    {
        return string.Equals(FileInfo1.FullName, FileInfo2.FullName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Does an early comparison by checking files Length, if lengths are not the same, files are definetely different
    /// </summary>
    /// <returns>true if different length</returns>
    private bool IsDifferentLength()
    {
        return FileInfo1.Length != FileInfo2.Length;
    }
}