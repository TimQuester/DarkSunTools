using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DarkSunRE.Commands;
using DarkSunRE.Models;
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

        public ImageResourceViewModel()
        {
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
