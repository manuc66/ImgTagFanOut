using DisplayMagician;

namespace WindowsThumbnails
{

    public class Program
    {
        public static void Main()
        {
            var enumerateFiles = Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "*.*", SearchOption.AllDirectories);
            foreach (var file in enumerateFiles)
            {
                Console.Write(file);
                var thumbnail = WindowsThumbnailProvider.GetThumbnail(file, 256, 256, ThumbnailOptions.WideThumbnails);
                Console.WriteLine(" .");
                thumbnail.Dispose();
            }
        }
    }
}