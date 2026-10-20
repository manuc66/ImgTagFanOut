using System;
using System.IO;
using Avalonia.Controls;
using Serilog;

namespace ImgTagFanOut.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            // Le fichier est livré à côté de l'exécutable, pas dans le dossier courant :
            // lancer l'application depuis le dossier du projet ne doit pas la faire planter.
            LicenseTextBlock.Text = ReadLicense();
        }

        private static string ReadLicense()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "LICENSE");

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warning(e, "Unable to read the license file at {Path}", path);
                return "LGPL-3.0 — https://github.com/manuc66/ImgTagFanOut/blob/main/LICENSE";
            }
        }
    }
}
