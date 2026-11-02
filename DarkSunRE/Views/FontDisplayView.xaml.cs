using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DarkSunRE.ViewModels;
using DarkSunRE.Models;

namespace DarkSunRE.Views
{
    /// <summary>
    /// Interaction logic for FontDisplayView.xaml
    /// </summary>
    public partial class FontDisplayView : UserControl
    {
        public FontDisplayView()
        {
            InitializeComponent();
        }

        private void GlyphImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not FontResourceViewModel viewModel ||
                !viewModel.IsPixelEditing ||
                sender is not Image image ||
                image.Source is not BitmapSource bitmap ||
                image.ActualWidth <= 0 ||
                image.ActualHeight <= 0 ||
                image.DataContext is not FontCharacter glyph)
            {
                return;
            }

            Point position = e.GetPosition(image);
            int x = (int)(position.X * bitmap.PixelWidth / image.ActualWidth);
            int y = (int)(position.Y * bitmap.PixelHeight / image.ActualHeight);
            viewModel.ToggleGlyphPixel(glyph.CharCode, x, y);
            e.Handled = true;
        }
    }
}