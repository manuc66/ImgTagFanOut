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
