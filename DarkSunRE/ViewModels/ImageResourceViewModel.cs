using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using DSunTools.Common.Gff;
using DSunTools.Common.Image;

namespace DarkSunRE.ViewModels
{
    public class ImageResourceViewModel : DocumentViewModel
    {
        private ImageSource _imageSource;
        private byte[] _imageData;
        private string _imagePath;
        private PaletteInfo _selectedPalette;
        private ObservableCollection<PaletteInfo> _availablePalettes;
        private Project _project;
        private ResourceInfo _resourceInfo;
        private double _scale = 1.0;
        private bool _useBmaCodec;
        private List<ImageFrame> _frames = new();
        private int _currentFrameIndex;
        private bool _isPlaying;
        private DispatcherTimer _timer;

        private readonly ICommand _playPauseCommand;
        private readonly ICommand _previousFrameCommand;
        private readonly ICommand _nextFrameCommand;
        private readonly ICommand _zoomInCommand;
        private readonly ICommand _zoomOutCommand;
        private readonly ICommand _resetZoomCommand;
        private readonly ICommand _exportPngCommand;
        private readonly ICommand _importPngCommand;
        private readonly IStatusService _statusService;

        public ImageResourceViewModel(IStatusService statusService = null)
        {
            _statusService = statusService;
            Title = "Image";
            ToolTip = "Image Resource";
            CanClose = true;
            AvailablePalettes = new ObservableCollection<PaletteInfo>();

            _playPauseCommand = new RelayCommand(TogglePlayback);
            _previousFrameCommand = new RelayCommand(PreviousFrame);
            _nextFrameCommand = new RelayCommand(NextFrame);
            _zoomInCommand = new RelayCommand(ZoomIn);
            _zoomOutCommand = new RelayCommand(ZoomOut);
            _resetZoomCommand = new RelayCommand(ResetZoom);
            _exportPngCommand = new RelayCommand(ExportPngFrames);
            _importPngCommand = new RelayCommand(ImportPngFrames);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, __) =>
            {
                if (FrameCount > 1)
                    CurrentFrameIndex = (CurrentFrameIndex + 1) % FrameCount;
            };
        }

        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }

        public byte[] ImageData
        {
            get => _imageData;
            set
            {
                _imageData = value;
                OnPropertyChanged();
                LoadFrames();
            }
        }

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
            }
        }

        public PaletteInfo SelectedPalette
        {
            get => _selectedPalette;
            set
            {
                if (ReferenceEquals(_selectedPalette, value)) return;
                _selectedPalette = value;
                OnPropertyChanged();
                RenderCurrentFrame();
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
                double clamped = Math.Max(0.1, Math.Min(10.0, value));
                if (Math.Abs(_scale - clamped) < 0.0001) return;
                _scale = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalePercentage));
                RenderCurrentFrame();
            }
        }

        public int ScalePercentage => (int)Math.Round(_scale * 100);

        public bool HasMultipleFrames => FrameCount > 1;
        public int FrameCount => _frames?.Count ?? 0;
        public int LastFrameIndex => Math.Max(0, FrameCount - 1);

        public int CurrentFrameIndex
        {
            get => _currentFrameIndex;
            set
            {
                int clamped = Math.Max(0, Math.Min(LastFrameIndex, value));
                if (_currentFrameIndex == clamped) return;
                _currentFrameIndex = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FrameLabel));
                RenderCurrentFrame();
            }
        }

        public string FrameLabel => FrameCount == 0
            ? string.Empty
            : $"{CurrentFrameIndex + 1} / {FrameCount}";

        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayPauseText));
                if (_isPlaying)
                    _timer.Start();
                else
                    _timer.Stop();
            }
        }

        public string PlayPauseText => IsPlaying ? "Pause" : "Play";

        public ICommand PlayPauseCommand => _playPauseCommand;
        public ICommand PreviousFrameCommand => _previousFrameCommand;
        public ICommand NextFrameCommand => _nextFrameCommand;
        public ICommand ZoomInCommand => _zoomInCommand;
        public ICommand ZoomOutCommand => _zoomOutCommand;
        public ICommand ResetZoomCommand => _resetZoomCommand;
        public ICommand ExportPngCommand => _exportPngCommand;
        public ICommand ImportPngCommand => _importPngCommand;

        public void Initialize(Project project, ResourceInfo resourceInfo, byte[] imageData)
        {
            _project = project;
            _resourceInfo = resourceInfo;
            _useBmaCodec = UsesBmaFrameCodec(project, resourceInfo);
            ImageData = imageData;

            // Загружаем доступные палитры из проекта
            LoadAvailablePalettes();

            // Устанавливаем первую палитру по умолчанию
            if (AvailablePalettes.Count > 0)
                SelectedPalette = AvailablePalettes[0];
        }

        public override void Reload()
        {
            LoadFrames();
        }

        public override void Save()
        {
            // Для изображений сохранение не требуется
        }

        private void ZoomIn() => Scale = Math.Min(10.0, _scale * 1.5);
        private void ZoomOut() => Scale = Math.Max(0.1, _scale / 1.5);
        private void ResetZoom() => Scale = 1.0;

        private void TogglePlayback()
        {
            if (FrameCount > 1)
                IsPlaying = !IsPlaying;
        }

        private void PreviousFrame()
        {
            if (FrameCount == 0) return;
            CurrentFrameIndex = CurrentFrameIndex == 0 ? LastFrameIndex : CurrentFrameIndex - 1;
        }

        private void NextFrame()
        {
            if (FrameCount == 0) return;
            CurrentFrameIndex = CurrentFrameIndex == LastFrameIndex ? 0 : CurrentFrameIndex + 1;
        }

        private void ExportPngFrames()
        {
            if (_resourceInfo == null || _frames == null || _frames.Count == 0)
            {
                _statusService?.ShowError("There are no image frames to export.", null);
                return;
            }

            Palette palette = _selectedPalette?.Palette;
            if (palette == null)
            {
                _statusService?.ShowError("Select a palette before exporting PNG frames.", null);
                return;
            }

            var dialog = new OpenFolderDialog { Title = "Select folder for image PNG frames" };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string baseName = GetFrameBaseName();
                for (int i = 0; i < _frames.Count; i++)
                {
                    ImageFrame frame = _frames[i];
                    string path = Path.Combine(dialog.FolderName, $"{baseName}_{i:D3}.png");
                    AnimationPngCodec.ExportFrame(
                        path,
                        frame.GetPixels(),
                        frame.Width,
                        frame.Height,
                        palette,
                        frame.GetAlphaMask());
                }

                _statusService?.ShowSuccess($"Exported {_frames.Count} PNG frame(s) to {dialog.FolderName}");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to export image frames: {ex.Message}", ex);
            }
        }

        private void ImportPngFrames()
        {
            if (_resourceInfo == null || _frames == null || _frames.Count == 0)
            {
                _statusService?.ShowError("There are no image frames to replace.", null);
                return;
            }

            Palette targetPalette = _selectedPalette?.Palette;
            if (targetPalette == null)
            {
                _statusService?.ShowError("Select a palette before importing PNG frames.", null);
                return;
            }

            var dialog = new OpenFolderDialog { Title = "Select folder with image PNG frames" };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string targetBase = GetFrameBaseName();
                var entries = Directory.GetFiles(dialog.FolderName, "*.png")
                    .Select(ParseFrameFile)
                    .OfType<PngFrameFile>()
                    .ToList();
                if (entries.Count == 0)
                    throw new InvalidDataException("No PNG frames with a numeric suffix were found (expected name_000.png).");

                var matching = entries
                    .Where(entry => string.Equals(entry.BaseName, targetBase, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.Number)
                    .ToList();
                if (matching.Count == 0)
                    matching = entries.OrderBy(entry => entry.Number).ToList();

                if (matching.GroupBy(entry => entry.Number).Any(group => group.Count() > 1))
                    throw new InvalidDataException($"The folder contains duplicate frame numbers for {targetBase}.");
                if (matching.Count != _frames.Count)
                {
                    throw new InvalidDataException(
                        $"This resource has {_frames.Count} frame(s), but {matching.Count} PNG frame(s) were supplied.");
                }

                int oldWidth = _frames[0].Width;
                int oldHeight = _frames[0].Height;
                var imported = new List<AnimationPngCodec.ImportedFrame>(matching.Count);
                foreach (PngFrameFile entry in matching)
                    imported.Add(AnimationPngCodec.ImportFrame(entry.Path));

                int width = imported[0].Width;
                int height = imported[0].Height;
                if (imported.Any(frame => frame.Width != width || frame.Height != height))
                    throw new InvalidDataException("All imported PNG frames must have the same dimensions.");

                if (_useBmaCodec && (width != oldWidth || height != oldHeight))
                {
                    throw new InvalidDataException(
                        $"BMA/CMAT frames must stay {oldWidth}x{oldHeight}; got {width}x{height}.");
                }

                var framePixels = new List<byte[]>(imported.Count);
                var alphaMasks = new List<BitArray?>(imported.Count);
                int nearestPixels = 0;
                foreach (AnimationPngCodec.ImportedFrame frame in imported)
                {
                    AnimationPngCodec.PaletteRemapResult remap =
                        AnimationPngCodec.RemapToPalette(frame, targetPalette);
                    framePixels.Add(remap.Pixels);
                    alphaMasks.Add(frame.AlphaMask);
                    nearestPixels += remap.NearestPixels;
                }

                byte[] encoded;
                if (_useBmaCodec && _imageData != null)
                {
                    BmaAnimation current = BmaReader.Read(_imageData);
                    List<int> layout = current.Containers.Select(container => container.FrameCount).ToList();
                    encoded = BmaWriter.Write(framePixels, width, height, layout);
                }
                else
                {
                    encoded = BitmapContainerWriter.Write(framePixels, width, height, alphaMasks);
                }

                ResourceFile container = _project?.Files?.FirstOrDefault(file =>
                    file.Resources != null &&
                    file.Resources.Any(resource =>
                        resource.Tag == _resourceInfo.Tag && resource.Number == _resourceInfo.Number));
                int resizedControls = 0;
                if (container != null)
                {
                    resizedControls = ButtonSizeSyncService.ApplyIconSizeChange(
                        _project,
                        container,
                        _resourceInfo,
                        oldWidth,
                        oldHeight,
                        width,
                        height);
                }

                _imageData = encoded;
                _resourceInfo.ModifiedData = encoded;
                _resourceInfo.IsModified = true;
                LoadFrames();
                IsModified = true;

                if (nearestPixels > 0)
                {
                    _statusService?.ShowWarning(
                        $"{nearestPixels} pixel(s) had no exact RGB match in the selected palette; nearest colors were used.");
                }

                string controlNote = resizedControls > 0
                    ? $" Updated {resizedControls} WIND/BUTN record(s) for the new button size."
                    : string.Empty;
                _statusService?.ShowSuccess(
                    $"Imported {imported.Count} PNG frame(s) into {_resourceInfo.Tag.Trim()}-{_resourceInfo.Number}." +
                    controlNote +
                    " Use Generate new file to write them into the GFF.");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to import image frames: {ex.Message}", ex);
            }
        }

        private sealed class PngFrameFile
        {
            public string Path { get; init; }
            public string BaseName { get; init; }
            public int Number { get; init; }
        }

        private static PngFrameFile? ParseFrameFile(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Match match = Regex.Match(name, @"^(.*)_(\d+)$");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int number))
                return null;

            return new PngFrameFile
            {
                Path = path,
                BaseName = match.Groups[1].Value,
                Number = number
            };
        }

        private string GetFrameBaseName()
        {
            string raw = _resourceInfo == null
                ? "image"
                : $"{_resourceInfo.Tag.Trim()}-{_resourceInfo.Number}";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(raw) ? "image" : raw;
        }

        private static bool UsesBmaFrameCodec(Project project, ResourceInfo resourceInfo)
        {
            if (resourceInfo?.Tag == "CMAT")
                return true;

            ResourceFile container = project?.Files?.FirstOrDefault(f =>
                f.Resources != null &&
                f.Resources.Any(r => r.Tag == resourceInfo?.Tag && r.Number == resourceInfo?.Number));

            return container?.Resources != null &&
                   container.Resources.Any(r => r.Tag == "BMA " || r.Tag == "ACF ");
        }

        private void LoadFrames()
        {
            IsPlaying = false;
            _frames = new List<ImageFrame>();

            if (_imageData != null && _imageData.Length > 0)
            {
                try
                {
                    if (_useBmaCodec)
                        _frames = BmaReader.Read(_imageData).Frames.ToList();
                    else
                        _frames = ImageReading.ExtractFrames(_imageData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode image frames: {ex.Message}");
                }
            }

            _currentFrameIndex = 0;
            OnPropertyChanged(nameof(FrameCount));
            OnPropertyChanged(nameof(HasMultipleFrames));
            OnPropertyChanged(nameof(LastFrameIndex));
            OnPropertyChanged(nameof(CurrentFrameIndex));
            OnPropertyChanged(nameof(FrameLabel));

            RenderCurrentFrame();
        }

        private void LoadAvailablePalettes()
        {
            if (_project?.Files == null)
                return;

            AvailablePalettes.Clear();

            // Ищем все палитры в файлах проекта
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
                            // Извлекаем данные палитры
                            var gff = GffFile.Create(file.Data);
                            var paletteData = gff.GetResourceData(resource.Tag, resource.Number);

                            if (paletteData != null && paletteData.Length == 768)
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
                            // Игнорируем ошибки при загрузке палитр
                            System.Diagnostics.Debug.WriteLine($"Failed to load palette {resource.Tag}-{resource.Number}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void RenderCurrentFrame()
        {
            if (_frames == null || _frames.Count == 0)
            {
                ImageSource = null;
                return;
            }

            if (_currentFrameIndex < 0 || _currentFrameIndex >= _frames.Count)
                _currentFrameIndex = 0;

            try
            {
                ImageFrame frame = _frames[_currentFrameIndex];
                var pixels = frame.GetPixels();
                var alphaMask = frame.GetAlphaMask();
                int width = frame.Width;
                int height = frame.Height;

                int scaledWidth = Math.Max(1, (int)Math.Round(width * _scale));
                int scaledHeight = Math.Max(1, (int)Math.Round(height * _scale));
                var pixelData = new byte[scaledWidth * scaledHeight * 4];

                for (int y = 0; y < scaledHeight; y++)
                {
                    int originalY = (int)(y / _scale);
                    if (originalY >= height) continue;

                    for (int x = 0; x < scaledWidth; x++)
                    {
                        int originalX = (int)(x / _scale);
                        if (originalX >= width) continue;

                        int pixelIndex = originalY * width + originalX;
                        int byteIndex = (y * scaledWidth + x) * 4;
                        byte paletteIndex = pixels[pixelIndex];

                        byte red, green, blue;
                        if (_selectedPalette?.Palette != null && paletteIndex < 256)
                        {
                            var color = _selectedPalette.Palette.GetColor(paletteIndex);
                            red = (byte)color.Red;
                            green = (byte)color.Green;
                            blue = (byte)color.Blue;
                        }
                        else
                        {
                            red = (byte)(paletteIndex * 4);
                            green = (byte)(paletteIndex * 2);
                            blue = paletteIndex;
                        }

                        pixelData[byteIndex] = blue;
                        pixelData[byteIndex + 1] = green;
                        pixelData[byteIndex + 2] = red;
                        pixelData[byteIndex + 3] = alphaMask.Get(pixelIndex) ? (byte)255 : (byte)0;
                    }
                }

                ImageSource = BitmapSource.Create(
                    scaledWidth,
                    scaledHeight,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixelData,
                    scaledWidth * 4);
            }
            catch (Exception ex)
            {
                ImageSource = null;
                System.Diagnostics.Debug.WriteLine($"Failed to render image frame: {ex.Message}");
            }
        }
    }

    public class PaletteInfo
    {
        public Palette Palette { get; set; }

        /// <summary>
        /// Per-frame palettes for ACF-driven animations; null for a normal
        /// static PAL entry.
        /// </summary>
        public IReadOnlyList<Palette> FramePalettes { get; set; }

        public string Name { get; set; }
        public string Tag { get; set; }
        public int Number { get; set; }
    }
}
