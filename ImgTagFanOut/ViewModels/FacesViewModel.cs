using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ImgTagFanOut.Dao;
using ImgTagFanOut.Models;
using ImgTagFanOut.Models.Faces;
using ReactiveUI;

namespace ImgTagFanOut.ViewModels;

/// <summary>
/// The strip of detected faces under the preview, used to name people.
/// A face carries at most one tag; naming a face also tags its image, so the
/// publish step keeps working with the existing tag model.
/// </summary>
public class FacesViewModel : ViewModelBase
{
    /// <summary>Large enough for the crops to stay readable, small enough to decode fast.</summary>
    private const uint CropSourceWidth = 1200;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly IPersonSuggester _personSuggester;
    private FaceItemViewModel? _selectedFace;
    private Tag? _selectedPersonTag;
    private string? _newPersonName;

    public FacesViewModel()
        : this(new UnitOfWorkFactory(), new ThumbnailProvider(), new PersonSuggester(new UnitOfWorkFactory())) { }

    internal FacesViewModel(
        IUnitOfWorkFactory unitOfWorkFactory,
        IThumbnailProvider thumbnailProvider,
        IPersonSuggester personSuggester)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _thumbnailProvider = thumbnailProvider;
        _personSuggester = personSuggester;

        IObservable<bool> hasFace = this.WhenAnyValue(x => x.SelectedFace).Select(face => face is not null);

        // Selecting a face pre-fills the suggested person: one click to accept.
        this.WhenAnyValue(x => x.SelectedFace)
            .Subscribe(face =>
            {
                string? name = face?.Person ?? face?.SuggestedPerson;
                SelectedPersonTag = name is null ? null : PersonTags.FirstOrDefault(tag => tag.Name == name);
            });

        AssignPersonCommand = ReactiveCommand.CreateFromTask(
            AssignSelectedPersonAsync,
            hasFace.CombineLatest(this.WhenAnyValue(x => x.SelectedPersonTag), (face, tag) => face && tag is not null));

        ClearPersonCommand = ReactiveCommand.CreateFromTask(
            ClearSelectedPersonAsync,
            this.WhenAnyValue(x => x.SelectedFace).Select(face => face?.Person is not null));

        CreatePersonCommand = ReactiveCommand.CreateFromTask(
            CreateAndAssignPersonAsync,
            hasFace.CombineLatest(this.WhenAnyValue(x => x.NewPersonName), (face, name) => face && !string.IsNullOrWhiteSpace(name)));
    }

    public ObservableCollection<FaceItemViewModel> Faces { get; } = new();

    public ObservableCollection<Tag> PersonTags { get; } = new();

    public FaceItemViewModel? SelectedFace
    {
        get => _selectedFace;
        set => this.RaiseAndSetIfChanged(ref _selectedFace, value);
    }

    public Tag? SelectedPersonTag
    {
        get => _selectedPersonTag;
        set => this.RaiseAndSetIfChanged(ref _selectedPersonTag, value);
    }

    public string? NewPersonName
    {
        get => _newPersonName;
        set => this.RaiseAndSetIfChanged(ref _newPersonName, value);
    }

    public bool HasFaces => Faces.Count > 0;

    /// <summary>Raised when a person tag was created, assigned or removed.</summary>
    public event EventHandler? TagsChanged;

    public ReactiveCommand<Unit, Unit> AssignPersonCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearPersonCommand { get; }
    public ReactiveCommand<Unit, Unit> CreatePersonCommand { get; }

    /// <summary>Image whose faces are currently displayed, used when naming a face.</summary>
    public CanHaveTag? Image { get; private set; }

    public string? Folder { get; private set; }

    private int _loadVersion;

    /// <summary>
    /// Refreshes the strip for the given image. Must run on the UI thread: the
    /// crops are <see cref="CroppedBitmap"/>, which are thread-affine.
    /// </summary>
    /// <summary>
    /// The label was removed from the image from the label panel: the faces that
    /// carried it no longer do, so the strip and the gallery must follow.
    /// </summary>
    public void OnPersonDetached(string personName)
    {
        foreach (FaceItemViewModel face in Faces.Where(face => face.Person == personName))
        {
            face.Person = null;
        }

        if (Folder is not null)
        {
            _personSuggester.Invalidate(Folder);
            ReloadPersonTags(Folder);
        }
    }

    public async Task LoadAsync(string? folder, CanHaveTag? image)
    {
        int version = ++_loadVersion;

        Image = image;
        Folder = folder;
        Faces.Clear();
        SelectedFace = null;
        NewPersonName = null;

        if (string.IsNullOrWhiteSpace(folder) || image is null)
        {
            ReloadPersonTags(null);
            this.RaisePropertyChanged(nameof(HasFaces));
            return;
        }

        IReadOnlyList<FaceRow> rows = await Task.Run(() => LoadRows(folder, image.Item));

        // Décodage hors du thread UI : 1200 px prennent quelques dizaines de millisecondes.
        string fullPath = image.GetFullFilePath(folder);
        Bitmap? source = rows.Count > 0
            ? await Task.Run(() => _thumbnailProvider.GetThumbnail(fullPath, CropSourceWidth))
            : null;

        // A newer selection won while the image was being decoded.
        if (version != _loadVersion)
        {
            return;
        }

        foreach (FaceRow row in rows)
        {
            Faces.Add(new FaceItemViewModel(
                row.Face.FaceId,
                row.Person,
                Crop(source, row.Face),
                row.SuggestedPerson,
                row.SuggestedScore));
        }

        ReloadPersonTags(folder);
        SelectedFace = Faces.FirstOrDefault();
        this.RaisePropertyChanged(nameof(HasFaces));
    }

    private IReadOnlyList<FaceRow> LoadRows(string folder, string itemName)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

        return unitOfWork.FaceRepository
            .GetFacesWithPerson(itemName)
            .Select(row => new FaceRow(
                row.Face,
                row.PersonName,
                row.PersonName is null ? Suggest(folder, row.Face) : null))
            .ToList();
    }

    private PersonMatch? Suggest(string folder, FaceDao face)
    {
        return face.Embedding is null
            ? null
            : _personSuggester.Suggest(folder, face.Embedding).FirstOrDefault() is { PersonName: not null } match
                ? match
                : null;
    }

    private void ReloadPersonTags(string? folder)
    {
        PersonTags.Clear();

        if (string.IsNullOrWhiteSpace(folder) || !_unitOfWorkFactory.DatabaseExists(folder))
        {
            return;
        }

        using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);
        foreach (Tag tag in unitOfWork.FaceRepository.GetPersonTags().Select(dao => new Tag(dao.Name)))
        {
            PersonTags.Add(tag);
        }
    }

    private sealed record FaceRow(FaceDao Face, string? Person, PersonMatch? Suggestion)
    {
        public string? SuggestedPerson => Suggestion?.PersonName;

        public float SuggestedScore => Suggestion?.Score ?? 0;
    }

    private static bool HasOtherFaceWithPerson(IUnitOfWork unitOfWork, string itemName, int faceId, string personName)
    {
        return unitOfWork.FaceRepository
            .GetFacesWithPerson(itemName)
            .Any(row => row.Face.FaceId != faceId && row.PersonName == personName);
    }

    private static IImage? Crop(Bitmap? source, FaceDao face)
    {
        if (source is null || source.PixelSize.Width == 0)
        {
            return null;
        }

        double width = source.PixelSize.Width;
        double height = source.PixelSize.Height;

        // A little margin around the face keeps the crop readable.
        double margin = 0.15 * Math.Max(face.Width, face.Height);
        int x = (int)Math.Max(0, (face.X - margin) * width);
        int y = (int)Math.Max(0, (face.Y - margin) * height);
        int w = (int)Math.Min(width - x, (face.Width + 2 * margin) * width);
        int h = (int)Math.Min(height - y, (face.Height + 2 * margin) * height);

        if (w <= 1 || h <= 1)
        {
            return null;
        }

        return new CroppedBitmap(source, new PixelRect(x, y, w, h));
    }

    private Task AssignSelectedPersonAsync()
    {
        return ApplyAsync(SelectedFace, SelectedPersonTag?.Name, create: false);
    }

    private Task ClearSelectedPersonAsync()
    {
        return ApplyAsync(SelectedFace, null, create: false);
    }

    private async Task CreateAndAssignPersonAsync()
    {
        string? name = NewPersonName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        await ApplyAsync(SelectedFace, name, create: true);
        NewPersonName = null;
    }

    private async Task ApplyAsync(FaceItemViewModel? face, string? personName, bool create)
    {
        if (face is null || Folder is null || Image is null)
        {
            return;
        }

        string folder = Folder;
        CanHaveTag image = Image;
        string? previousPerson = face.Person;

        await Task.Run(() =>
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.GetUnitOfWork(folder);

            TagDao? tagDao = personName is null ? null : unitOfWork.TagRepository.GetTagDao(personName);

            if (tagDao is null && create && personName is not null)
            {
                unitOfWork.TagRepository.TryCreateTag(personName, out _);
                unitOfWork.SaveChanges();
                tagDao = unitOfWork.TagRepository.GetTagDao(personName);
            }

            FaceDao? faceDao = unitOfWork.FaceRepository.GetFace(face.FaceId);
            if (faceDao is null)
            {
                return;
            }

            unitOfWork.FaceRepository.SetPersonTag(faceDao, tagDao);

            if (tagDao is not null)
            {
                // Naming a face also tags its image, otherwise publishing would ignore it.
                unitOfWork.TagRepository.AddTagToItem(new Tag(tagDao.Name), image);
            }
            else if (previousPerson is not null
                     && !HasOtherFaceWithPerson(unitOfWork, image.Item, face.FaceId, previousPerson))
            {
                // The label was only there for that face: remove it from the photo too.
                unitOfWork.TagRepository.RemoveTagToItem(new Tag(previousPerson), image);
            }

            unitOfWork.SaveChanges();
        });

        face.Person = personName;
        _personSuggester.Invalidate(folder);
        ReloadPersonTags(folder);
        SelectedPersonTag = PersonTags.FirstOrDefault(tag => tag.Name == personName);

        // Person names are tags like the others: the label panel must show them.
        TagsChanged?.Invoke(this, EventArgs.Empty);
    }
}
