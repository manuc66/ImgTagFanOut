using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ImgTagFanOut.Dao;

public class TagDao
{
    [Key]
    public int TagId { get; set; }
    public string Name { get; set; }

    public List<ItemTagDao> ItemTags { get; } = new();
    public List<ItemDao> Items { get; } = new();
}