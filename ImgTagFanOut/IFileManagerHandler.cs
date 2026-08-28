using System.Threading.Tasks;

namespace ImgTagFanOut;

public interface IFileManagerHandler
{
    Task OpenParentFolder(string path);
    Task OpenFolder(string parentFolder);
    Task OpenFile(string path);
    Task RevealFileInFolder(string path);
}