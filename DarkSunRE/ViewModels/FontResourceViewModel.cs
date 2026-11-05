using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using DSunTools.Common.Font;

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
        private FontParser _fontParser;
        private byte[] _originalFontData = Array.Empty<byte>();
        private Dictionary<int, FontCharacter> _baseCharacters = new();
        private readonly IStatusService _statusService;
        private string _validationError = string.Empty;
        private bool _isPixelEditing;
        private Palette _selectedPalette;
        private PaletteInfo _selectedPaletteInfo;
        private ObservableCollection<PaletteInfo> _availablePalettes;
        private bool _isTransparent = false;
        private const double Opacity = 0.5; // Постоянное значение прозрачности 50%
        private const int TargetColorIndex = 20; // Цвет с индексом 20 будет полупрозрачным

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

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                _validationError = value;
                OnPropertyChanged();
            }
        }

        public bool IsPixelEditing
        {
            get => _isPixelEditing;
            set
            {
                _isPixelEditing = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand ExportAllGlyphsCommand { get; }

        public string GlyphOverridesPath => GetGlyphOverridesPath();
        public string GlyphExportPath => GetGlyphExportPath();

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
        
        public bool IsTransparent
        {
            get => _isTransparent;
            set
            {
                _isTransparent = value;
                OnPropertyChanged();
                RenderAllCharacterImages(); // Re-render all characters with new transparency setting
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

        public FontResourceViewModel(IStatusService statusService)
        {
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
            Title = "Font";
            ToolTip = "Font Resource";
            CanClose = true;
            Characters = new ObservableCollection<FontCharacter>();
            AvailablePalettes = new ObservableCollection<PaletteInfo>();
            _zoomInCommand = new RelayCommand(ZoomIn);
            _zoomOutCommand = new RelayCommand(ZoomOut);
            _resetZoomCommand = new RelayCommand(ResetZoom);
            SaveCommand = new RelayCommand(Save, () => IsModified && _resourceInfo != null && _fontParser != null && !string.IsNullOrEmpty(GetGlyphOverridesPath()) && string.IsNullOrEmpty(ValidationError));
            ReloadCommand = new RelayCommand(Reload, () => IsModified);
            ExportAllGlyphsCommand = new RelayCommand(ExportAllGlyphs, CanExportAllGlyphs);
        }

        public void Initialize(Project project, ResourceInfo resourceInfo, byte[] fontData)
        {
            _project = project;
            _resourceInfo = resourceInfo ?? throw new ArgumentNullException(nameof(resourceInfo));
            _originalFontData = fontData == null ? Array.Empty<byte>() : (byte[])fontData.Clone();
            FontData = (byte[])_originalFontData.Clone();
            IsModified = false;
            
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
                _fontParser = new FontParser();
                var characters = _fontParser.ParseFontData(_fontData);
                _baseCharacters = characters.ToDictionary(character => character.CharCode, CloneGlyph);
                ValidationError = string.Empty;
                ApplyGlyphOverrides(characters);

                // Apply transliteration if available
                if (_project?.TransliterationRules != null)
                {
                    foreach (var character in characters)
                    {
                        var rule = _project.TransliterationRules
                            .FirstOrDefault(candidate => candidate.From?.Length == 1 && candidate.From[0] == character.CharCode);
                        if (rule != null)
                            character.Transliteration = rule.To;
                    }
                }

                Characters = new ObservableCollection<FontCharacter>(characters);
                RenderAllCharacterImages();
            }
            catch (Exception ex)
            {
                _fontParser = null;
                ValidationError = ex.Message;
                Characters = new ObservableCollection<FontCharacter>();
                System.Diagnostics.Debug.WriteLine($"Failed to parse font data: {ex.Message}");
            }
        }

        public void ToggleGlyphPixel(int characterCode, int x, int y)
        {
            if (!IsPixelEditing)
                return;

            FontCharacter glyph = Characters.FirstOrDefault(character => character.CharCode == characterCode);
            if (glyph?.Pixels == null || y < 0 || y >= glyph.Pixels.GetLength(0) || x < 0 || x >= glyph.Pixels.GetLength(1))
                return;

            glyph.Pixels[y, x] = glyph.Pixels[y, x] == 0 ? (byte)0xFE : (byte)0;
            glyph.IsOverridden = !MatchesBaseGlyph(glyph);
            RenderAllCharacterImages();
            IsModified = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private void RenderAllCharacterImages()
        {
            if (Characters == null)
                return;

            System.Diagnostics.Debug.WriteLine($"Rendering all character images, IsTransparent = {_isTransparent}");
            int renderedCount = 0;

            // Create new list of characters with updated images
            var updatedCharacters = new List<FontCharacter>();
            foreach (var character in Characters)
            {
                var updatedCharacter = new FontCharacter
                {
                    CharCode = character.CharCode,
                    Width = character.Width,
                    Pixels = character.Pixels,
                    Transliteration = character.Transliteration,
                    IsOverridden = character.IsOverridden,
                    Image = RenderCharacterImage(character)
                };
                updatedCharacters.Add(updatedCharacter);
                renderedCount++;
            }
            
            // Replace the entire collection to force UI update
            Characters = new ObservableCollection<FontCharacter>(updatedCharacters);
            System.Diagnostics.Debug.WriteLine($"Rendered {renderedCount} character images, IsTransparent = {_isTransparent}");
        }

        private void ApplyGlyphOverrides(List<FontCharacter> characters)
        {
            string directory = GetGlyphOverridesPath();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            var errors = new List<string>();
            foreach (FontCharacter character in characters)
            {
                string path = Path.Combine(directory, $"glyph_{character.CharCode}.png");
                if (!System.IO.File.Exists(path))
                    continue;

                try
                {
                    FontCharacter replacement = LoadGlyphPng(path, character.CharCode);
                    character.Width = replacement.Width;
                    character.Pixels = replacement.Pixels;
                    character.IsOverridden = !MatchesBaseGlyph(character);
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
                ValidationError = "Could not load glyph overrides: " + string.Join("; ", errors);
        }

        private static FontCharacter LoadGlyphPng(string path, int characterCode)
        {
            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("The PNG contains no image frame.");

            BitmapSource source = decoder.Frames[0];
            if (source.PixelHeight != FontResourceCodec.GlyphHeight || source.PixelWidth is < 1 or > ushort.MaxValue)
                throw new InvalidDataException($"Glyph PNG must be 1..65535 pixels wide and {FontResourceCodec.GlyphHeight} pixels high.");

            var grayscale = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            int width = grayscale.PixelWidth;
            byte[] flatPixels = new byte[checked(width * FontResourceCodec.GlyphHeight)];
            grayscale.CopyPixels(flatPixels, width, 0);
            var pixels = new byte[FontResourceCodec.GlyphHeight, width];
            for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y, x] = flatPixels[y * width + x];
            }

            return new FontCharacter { CharCode = characterCode, Width = width, Pixels = pixels };
        }

        private static void SaveGlyphPng(FontCharacter glyph, string path)
        {
            if (glyph.Width <= 0 || glyph.Width > ushort.MaxValue || glyph.Pixels == null ||
                glyph.Pixels.GetLength(0) != FontResourceCodec.GlyphHeight || glyph.Pixels.GetLength(1) != glyph.Width)
            {
                throw new InvalidDataException($"Glyph {glyph.CharCode:X2} has invalid pixel dimensions.");
            }

            byte[] flatPixels = new byte[checked(glyph.Width * FontResourceCodec.GlyphHeight)];
            for (int y = 0; y < FontResourceCodec.GlyphHeight; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                    flatPixels[y * glyph.Width + x] = glyph.Pixels[y, x];
            }

            var bitmap = new WriteableBitmap(glyph.Width, FontResourceCodec.GlyphHeight, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, glyph.Width, FontResourceCodec.GlyphHeight), flatPixels, glyph.Width, 0);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = System.IO.File.Create(path);
            encoder.Save(stream);
        }

        private static FontCharacter CloneGlyph(FontCharacter glyph)
        {
            var pixels = new byte[glyph.Pixels.GetLength(0), glyph.Pixels.GetLength(1)];
            Array.Copy(glyph.Pixels, pixels, glyph.Pixels.Length);
            return new FontCharacter
            {
                CharCode = glyph.CharCode,
                Width = glyph.Width,
                Pixels = pixels,
                Transliteration = glyph.Transliteration,
                IsOverridden = glyph.IsOverridden
            };
        }

        private bool MatchesBaseGlyph(FontCharacter glyph)
        {
            if (!_baseCharacters.TryGetValue(glyph.CharCode, out FontCharacter original) ||
                glyph.Width != original.Width || glyph.Pixels == null || original.Pixels == null ||
                glyph.Pixels.GetLength(0) != original.Pixels.GetLength(0) ||
                glyph.Pixels.GetLength(1) != original.Pixels.GetLength(1))
            {
                return false;
            }

            for (int y = 0; y < glyph.Pixels.GetLength(0); y++)
            {
                for (int x = 0; x < glyph.Pixels.GetLength(1); x++)
                {
                    if (glyph.Pixels[y, x] != original.Pixels[y, x])
                        return false;
                }
            }

            return true;
        }

        private string GetGlyphOverridesPath()
        {
            return string.IsNullOrWhiteSpace(_project?.ModifiedGamePath)
                ? string.Empty
                : Path.Combine(_project.ModifiedGamePath, "Glyphs", "Overrides");
        }

        private string GetGlyphExportPath()
        {
            return string.IsNullOrWhiteSpace(_project?.ModifiedGamePath)
                ? string.Empty
                : Path.Combine(_project.ModifiedGamePath, "Glyphs", "Export");
        }

        private bool CanExportAllGlyphs()
        {
            return Characters?.Count == FontResourceCodec.GlyphCount && !string.IsNullOrEmpty(GetGlyphExportPath());
        }

        private void ExportAllGlyphs()
        {
            try
            {
                string directory = GetGlyphExportPath();
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException("The project output folder is not configured.");

                Directory.CreateDirectory(directory);
                int exportedCount = 0;
                var manifest = new StringBuilder("Code,Width,File");
                manifest.AppendLine();
                foreach (FontCharacter character in Characters)
                {
                    string fileName = character.Width == 0 ? string.Empty : $"glyph_{character.CharCode}.png";
                    manifest.AppendLine($"{character.CharCode},{character.Width},{fileName}");

                    string filePath = Path.Combine(directory, $"glyph_{character.CharCode}.png");
                    if (character.Width == 0)
                    {
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);
                        continue;
                    }

                    SaveGlyphPng(character, filePath);
                    exportedCount++;
                }

                System.IO.File.WriteAllText(Path.Combine(directory, "glyph_widths.csv"), manifest.ToString());
                System.IO.File.WriteAllText(Path.Combine(directory, "README.txt"),
                    "glyph_widths.csv lists all 256 glyph codes and widths. Non-empty glyphs are 8-bit grayscale PNGs named glyph_<code>.png, exactly 9 pixels high; each grayscale value is the original pixel value. " +
                    "Zero-width glyphs have no PNG because PNG images cannot have zero width. Missing PNGs leave the original glyph unchanged. Copy selected PNGs into the project's Glyphs\\Overrides folder to use them as replacements.\r\n");
                _statusService.ShowSuccess($"Exported {Characters.Count} glyph entries ({exportedCount} PNGs) to {directory}");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to export glyphs: {ex.Message}", ex);
            }
        }

        private void PersistGlyphOverrides()
        {
            string directory = GetGlyphOverridesPath();
            var changedGlyphs = Characters.Where(glyph => !MatchesBaseGlyph(glyph)).ToList();
            if (changedGlyphs.Count > 0)
                Directory.CreateDirectory(directory);

            foreach (FontCharacter glyph in Characters)
            {
                string path = Path.Combine(directory, $"glyph_{glyph.CharCode}.png");
                if (MatchesBaseGlyph(glyph) || glyph.Width == 0)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                    glyph.IsOverridden = false;
                    continue;
                }

                SaveGlyphPng(glyph, path);
                glyph.IsOverridden = true;
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
                
                // Create WriteableBitmap
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                
                // Create pixel array
                var pixelData = new byte[width * height * 4]; // 4 bytes per pixel (BGRA)

                // Fill pixels
                int transparentPixels = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int pixelValue = character.Pixels[y, x];
                        int pixelIndex = y * width + x;
                        int byteIndex = pixelIndex * 4; // 4 bytes per pixel (BGRA)

                        // Check if this pixel should be transparent (black color = 0)
                        bool isBackgroundPixel = (pixelValue == 0) && _isTransparent;

                        // Use palette or default grayscale
                        byte red, green, blue, alpha;
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
                        
                        // Set alpha based on transparency setting
                        // Apply opacity only to target color pixels
                        if (pixelValue == TargetColorIndex && _isTransparent)
                        {
                            alpha = isBackgroundPixel ? (byte)0 : (byte)(255 * Opacity);
                        }
                        else
                        {
                            alpha = isBackgroundPixel ? (byte)0 : (byte)255;
                        }
                        if (alpha == 0) transparentPixels++;

                        // Write pixel to array (BGRA format)
                        pixelData[byteIndex] = blue;     // B
                        pixelData[byteIndex + 1] = green; // G
                        pixelData[byteIndex + 2] = red;   // R
                        pixelData[byteIndex + 3] = alpha;   // A
                    }
                }
                
                // Debug output
                if (_isTransparent && transparentPixels > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Character {character.CharCode} has {transparentPixels} transparent pixels");
                }

                // Write pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixelData, width * 4, 0);

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
            ValidationError = string.Empty;
            FontData = (byte[])_originalFontData.Clone();
            IsModified = false;
            CommandManager.InvalidateRequerySuggested();
        }

        public override void Save()
        {
            if (_resourceInfo == null || _fontParser == null)
            {
                ValidationError = "The FONT resource is not initialized or could not be parsed.";
                return;
            }

            if (string.IsNullOrEmpty(GetGlyphOverridesPath()))
            {
                ValidationError = "The project output folder is not configured.";
                return;
            }

            try
            {
                byte[] updatedData = _fontParser.BuildFontData(Characters.ToList());
                PersistGlyphOverrides();
                _resourceInfo.ModifiedData = updatedData;
                _resourceInfo.IsModified = true;
                IsModified = false;
                ValidationError = string.Empty;
                _statusService.ShowSuccess($"Font built; glyph overrides saved to {GetGlyphOverridesPath()}");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                ValidationError = $"Failed to save font: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(ValidationError);
            }
        }
    }
}
