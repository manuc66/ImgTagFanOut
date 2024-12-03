using System.Diagnostics;

namespace ImgTagFanOut.Dao;

[DebuggerDisplay("{OrderIndex}-{ItemForeignKey}-{TagForeignKey}")]
public class ItemTagDao
{
    public int ItemForeignKey { get; set; }
    public int TagForeignKey { get; set; }
    public int OrderIndex { get; set; }
    public ItemDao Item { get; set; } = null!;
    public TagDao Tag { get; set; } = null!;
}
