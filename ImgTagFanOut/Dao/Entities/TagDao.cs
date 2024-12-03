using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

[DebuggerDisplay("{Name}#{TagId}")]
public class TagDao
{
    [Key]
    public int TagId { get; set; }
    public string Name { get; set; }

    public TagDao(string name)
    {
        Name = name;
    }

    public List<ItemTagDao> ItemTags { get; } = new();
    public List<ItemDao> Items { get; } = new();
}
