using Avalonia.Controls;

namespace ImgTagFanOut.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            var licenseText = System.IO.File.ReadAllText("LICENSE");
            LicenseTextBlock.Text = licenseText;
        }
    }
}
