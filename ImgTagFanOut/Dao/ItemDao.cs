using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ImgTagFanOut.Dao;

public class ItemDao
{
    [Key]
    public int ItemId { get; set; }
    
    public string Name { get; set; }

    public List<ItemTagDao> ItemTags { get; } = new();
    public List<TagDao> Tags { get; } = new();
}