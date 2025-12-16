using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSunTools.Common.Image;
using DSunTools.Common.Gff;

namespace DarkSunRE.ViewModels
{
    public class FontResourceViewModel : DocumentViewModel
    {
        private byte[] _fontData;
        private ObservableCollection<FontCharacter> _characters;
        private double _scale = 1.0;
        private ICommand _zoomInCommand;
        private ICommand _zoomOutCommand;
        private ICommand _resetZoomCommand;
        private Project _project;
        private ResourceInfo _resourceInfo;
        private Palette _selectedPalette;
        private PaletteInfo _selectedPaletteInfo;
        private ObservableCollection<PaletteInfo> _availablePalettes;

        public byte[] FontData
        {
            get => _fontData;
            set
            {
                _fontData = value;
                OnPropertyChanged();
                ParseFontData();
            }
        }

        public ObservableCollection<FontCharacter> Characters
        {
            get => _characters;
            set
            {
                _characters = value;
                OnPropertyChanged();
            }
        }

        public PaletteInfo SelectedPaletteInfo
        {
            get => _selectedPaletteInfo;
            set
            {
                _selectedPaletteInfo = value;
                _selectedPalette = value?.Palette;
                OnPropertyChanged();
                RenderAllCharacterImages(); // Re-render all characters with new palette
                OnPropertyChanged(nameof(Characters)); // Notify that character images have changed
            }
        }

        public ObservableCollection<PaletteInfo> AvailablePalettes
        {
            get => _availablePalettes;
            set
            {
                _availablePalettes = value;
                OnPropertyChanged();
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalePercentage));
                OnPropertyChanged(nameof(CellWidth));
                OnPropertyChanged(nameof(CellHeight));
                OnPropertyChanged(nameof(CharacterFontSize));
                OnPropertyChanged(nameof(TransliterationFontSize));
                OnPropertyChanged(nameof(Characters)); // Notify that character images have changed
            }
        }

        public int ScalePercentage => (int)(_scale * 100);
        
        // Свойства для управления размерами ячеек и шрифтов
        public double CellWidth => 80 * _scale;
        public double CellHeight => 100 * _scale;
        public double CharacterFontSize => 14 * Math.Sqrt(_scale); // Less aggressive scaling for text
        public double TransliterationFontSize => 12 * Math.Sqrt(_scale); // Less aggressive scaling for text

        public ICommand ZoomInCommand => _zoomInCommand ?? (_zoomInCommand = new RelayCommand(ZoomIn));
        public ICommand ZoomOutCommand => _zoomOutCommand ?? (_zoomOutCommand = new RelayCommand(ZoomOut));
        public ICommand ResetZoomCommand => _resetZoomCommand ?? (_resetZoomCommand = new RelayCommand(ResetZoom));

        public FontResourceViewModel()
        {
            Title = "Font";
            ToolTip = "Font Resource";
            CanClose = true;
            Characters = new ObservableCollection<FontCharacter>();
            AvailablePalettes = new ObservableCollection<PaletteInfo>();
            _zoomInCommand = new RelayCommand(ZoomIn);
            _zoomOutCommand = new RelayCommand(ZoomOut);
            _resetZoomCommand = new RelayCommand(ResetZoom);
        }

        public void Initialize(Project project, ResourceInfo resourceInfo, byte[] fontData)
        {
            _project = project;
            _resourceInfo = resourceInfo;
            FontData = fontData;
            
            // Load available palettes from the project
            LoadAvailablePalettes();
            
            // Set the first palette as default
            if (AvailablePalettes.Count > 0)
            {
                SelectedPaletteInfo = AvailablePalettes[0];
            }
        }

        private void LoadAvailablePalettes()
        {
            if (_project?.Files == null)
            {
                AvailablePalettes.Clear();
                return;
            }

            AvailablePalettes.Clear();

            // Look for all available palettes in the project
            foreach (var file in _project.Files)
            {
                if (file.Resources == null)
                    continue;

                foreach (var resource in file.Resources)
                {
                    if (resource.Type == ResourceType.Palette)
                    {
                        try
                        {
                            // Extract palette data
                            var gff = GffFile.Create(file.Data);
                            var paletteData = gff.GetResourceData(resource.Tag, resource.Number);
                            
                            if (paletteData != null && paletteData.Length == 768) // PAL files usually 768 bytes
                            {
                                var palette = Palette.FromPalData(paletteData);
                                var paletteInfo = new PaletteInfo
                                {
                                    Palette = palette,
                                    Name = $"{resource.Tag}-{resource.Number}",
                                    Tag = resource.Tag,
                                    Number = resource.Number
                                };
                                AvailablePalettes.Add(paletteInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Ignore errors when loading palettes
                            System.Diagnostics.Debug.WriteLine($"Failed to load palette {resource.Tag}-{resource.Number}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void ParseFontData()
        {
            if (_fontData == null || _fontData.Length == 0)
                return;

            try
            {
                var parser = new FontParser();
                var characters = parser.ParseFontData(_fontData);
                
                // Apply transliteration if available
                if (_project?.TransliterationRules != null)
                {
                    foreach (var character in characters)
                    {
                        if (!character.IsControlCharacter)
                        {
                            // Find transliteration rule for this character
                            var rule = _project.TransliterationRules
                                .FirstOrDefault(r => r.From == character.LatinCharacter);
                            if (rule != null)
                            {
                                character.Transliteration = rule.To;
                            }
                        }
                    }
                }
                
                Characters = new ObservableCollection<FontCharacter>(characters);
                RenderAllCharacterImages();
            }
            catch (Exception ex)
            {
                // Handle parsing error
                System.Diagnostics.Debug.WriteLine($"Failed to parse font data: {ex.Message}");
            }
        }

        private void RenderAllCharacterImages()
        {
            if (Characters == null)
                return;

            // Only re-render images when scale changes, not on every property change
            foreach (var character in Characters)
            {
                character.Image = RenderCharacterImage(character);
            }
        }

        private void ZoomIn()
        {
            if (_scale < 10.0)
            {
                Scale = Math.Min(_scale * 1.5, 10.0);
            }
        }

        private void ZoomOut()
        {
            if (_scale > 0.1)
            {
                Scale = Math.Max(_scale / 1.5, 0.1);
            }
        }

        private void ResetZoom()
        {
            Scale = 1.0;
        }

        public ImageSource RenderCharacterImage(FontCharacter character)
        {
            if (character.IsEmpty || character.Pixels == null)
                return null;

            try
            {
                int width = character.Width;
                int height = 9; // Fixed height for font characters
                
                // Create pixel array for BitmapSource (at original size)
                var pixelData = new byte[width * height * 4]; // 4 bytes per pixel (BGRA)

                // Fill pixels
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int pixelValue = character.Pixels[y, x];
                        int pixelIndex = y * width + x;
                        int byteIndex = pixelIndex * 4; // 4 bytes per pixel (BGRA)

                        // Use palette or default grayscale
                        byte red, green, blue;
                        if (_selectedPalette != null && pixelValue < 256)
                        {
                            var color = _selectedPalette.GetColor((byte)pixelValue);
                            red = (byte)color.Red;
                            green = (byte)color.Green;
                            blue = (byte)color.Blue;
                        }
                        else
                        {
                            // Default grayscale
                            red = green = blue = (byte)(pixelValue * 4);
                        }

                        // Write pixel to array (BGRA format)
                        pixelData[byteIndex] = blue;     // B
                        pixelData[byteIndex + 1] = green; // G
                        pixelData[byteIndex + 2] = red;   // R
                        pixelData[byteIndex + 3] = 255;   // A (fully opaque)
                    }
                }

                // Create BitmapSource from pixel array
                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96, // DPI X
                    96, // DPI Y
                    PixelFormats.Bgra32,
                    null, // palette
                    pixelData,
                    width * 4 // stride
                );

                return bitmap;
            }
            catch (Exception ex)
            {
                // Return null in case of error
                System.Diagnostics.Debug.WriteLine($"Failed to render character image: {ex.Message}");
                return null;
            }
        }

        public override void Reload()
        {
            ParseFontData();
        }

        public override void Save()
        {
            // Font saving not implemented yet
        }
    }
}
