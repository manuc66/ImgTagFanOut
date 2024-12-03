using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImgTagFanOut.Dao;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    public ITagRepository TagRepository { get; }
    public IParameterRepository ParameterRepository { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    int SaveChanges();
}
