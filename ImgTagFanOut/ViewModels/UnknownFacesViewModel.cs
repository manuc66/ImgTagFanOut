using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>
/// Groups the faces nobody named yet, so a dozen names cover a hundred faces.
/// Naming a cluster links every face in it and tags their photos.
/// </summary>
public class UnknownFacesViewModel : ViewModelBase
{
    /// <summary>Enough to make progress without turning the tab into a wall of photos.</summary>
    private const int MaxClusters = 60;

    /// <summary>Wide enough for a readable crop without decoding the full photo.</summary>
    private const uint ThumbnailWidth = 800;

    private readonly Func<string?> _workingFolderGetter;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly FaceClusterer _clusterer = new();
    private bool _isBusy;
    private string? _statusMessage;

    public UnknownFacesViewModel()
        : this(() => null, new UnitOfWorkFactory(), new ThumbnailProvider()) { }

    internal UnknownFacesViewModel(
        Func<string?> workingFolderGetter,
        IUnitOfWorkFactory unitOfWorkFactory,
        IThumbnailProvider thumbnailProvider)
    {
        _workingFolderGetter = workingFolderGetter;
        _unitOfWorkFactory = unitOfWorkFactory;
        _thumbnailProvider = thumbnailProvider;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        NameClusterCommand = ReactiveCommand.CreateFromTask<UnknownClusterViewModel, Unit>(
            NameClusterAsync,
            this.WhenAnyValue(x => x.IsBusy).Select(busy => !busy));
    }

    public ObservableCollection<UnknownClusterViewModel> Clusters { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsEmpty => !IsBusy && Clusters.Count == 0;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<UnknownClusterViewModel, Unit> NameClusterCommand { get; }

    /// <summary>Raised after a cluster was named, so the label panel can refresh.</summary>
    public event EventHandler? TagsChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        string? folder = _workingFolderGetter();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        Clusters.Clear();

        try
        {
            List<FaceEmbedding> unnamed = await Task.Run(() =>
            {
                using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);
                return unitOfWork.FaceRepository.GetUnnamedFaces();
            }, cancellationToken);

            IReadOnlyList<FaceCluster> clusters = _clusterer.Cluster(unnamed);

            // Un décodage par photo, pas par visage : plusieurs visages viennent souvent du même fichier.
            Dictionary<string, Bitmap?> thumbnails = new(StringComparer.Ordinal);

            foreach (FaceCluster cluster in clusters.Take(MaxClusters))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string item = cluster.Representative.ItemName;
                if (!thumbnails.TryGetValue(item, out Bitmap? source))
                {
                    string fullPath = Path.Combine(folder, item);
                    source = await Task.Run(() => _thumbnailProvider.GetThumbnail(fullPath, ThumbnailWidth), cancellationToken);
                    thumbnails[item] = source;
                }

                FaceEmbedding representative = cluster.Representative;
                IImage? crop = FaceCrop.Create(source, representative.X, representative.Y, representative.Width, representative.Height);
                Clusters.Add(new UnknownClusterViewModel(cluster, crop));
            }

            StatusMessage = string.Format(Resources.Resources.UnknownFacesFound, unnamed.Count, Clusters.Count);
        }
        finally
        {
            IsBusy = false;
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task<Unit> NameClusterAsync(UnknownClusterViewModel cluster, CancellationToken cancellationToken)
    {
        string? folder = _workingFolderGetter();
        string? person = cluster.PersonName?.Trim();

        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrEmpty(person))
        {
            return Unit.Default;
        }

        await Task.Run(() =>
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

            TagDao? tag = unitOfWork.TagRepository.GetTagDao(person);
            if (tag is null)
            {
                // A cluster is usually a new person: create the tag on the way.
                unitOfWork.TagRepository.TryCreateTag(person, out _);
                unitOfWork.SaveChanges();
                tag = unitOfWork.TagRepository.GetTagDao(person);
            }

            if (tag is null)
            {
                return;
            }

            foreach (FaceEmbedding face in cluster.Cluster.Faces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FaceDao? stored = unitOfWork.FaceRepository.GetFace(face.FaceId);
                if (stored is null)
                {
                    continue;
                }

                unitOfWork.FaceRepository.SetPersonTag(stored, tag);
                unitOfWork.TagRepository.AddTagToItem(new Tag(person), new CanHaveTag(face.ItemName));
            }

            unitOfWork.SaveChanges();
        }, cancellationToken);

        int count = cluster.Count;
        Clusters.Remove(cluster);
        this.RaisePropertyChanged(nameof(IsEmpty));
        StatusMessage = string.Format(Resources.Resources.ClusterNamed, count, person);
        TagsChanged?.Invoke(this, EventArgs.Empty);

        return Unit.Default;
    }
}

/// <summary>One group of look-alike unknown faces.</summary>
public class UnknownClusterViewModel : ViewModelBase
{
    private string? _personName;

    public UnknownClusterViewModel(FaceCluster cluster, IImage? image)
    {
        Cluster = cluster;
        Image = image;
    }

    public FaceCluster Cluster { get; }

    public IImage? Image { get; }

    public int Count => Cluster.Count;

    public string Label => string.Format(Resources.Resources.ClusterFaces, Count);

    /// <summary>Name typed by the user; an existing person or a new one.</summary>
    public string? PersonName
    {
        get => _personName;
        set => this.RaiseAndSetIfChanged(ref _personName, value);
    }
}
