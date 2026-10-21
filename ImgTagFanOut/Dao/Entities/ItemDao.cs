using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

[DebuggerDisplay("{Name}#{ItemId}")]
public class ItemDao
{
    [Key]
    public int ItemId { get; set; }

    public string Name { get; set; }
    public string? Hash { get; set; }

    /// <summary>Taille du fichier au dernier scan, pour éviter de le re-hacher.</summary>
    public long Size { get; set; }

    /// <summary>Date de dernière écriture (ticks UTC) au dernier scan.</summary>
    public long ModifiedAtUtcTicks { get; set; }

    public bool Done { get; set; }

    public List<ItemTagDao> ItemTags { get; } = new();
    public List<TagDao> Tags { get; } = new();
    public List<FaceDao> Faces { get; } = new();
    public FaceAnalysisDao? FaceAnalysis { get; set; }

    public ItemDao(string name)
    {
        Name = name;
    }
}
