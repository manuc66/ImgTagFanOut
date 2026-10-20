using Microsoft.EntityFrameworkCore;

namespace ImgTagFanOut.Dao;

public interface IImgTagFanOutDbContext
{
    DbSet<TagDao> Tags { get; }
    DbSet<ItemDao> Items { get; }
    DbSet<ItemTagDao> ItemTags { get; }
    DbSet<ParameterDao> Parameters { get; }
    DbSet<FaceDao> Faces { get; }
    DbSet<FaceAnalysisDao> FaceAnalyses { get; }
}
