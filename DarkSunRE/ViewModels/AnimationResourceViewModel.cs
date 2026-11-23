using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// <summary>
    /// Viewer for DS1 BMA cinematic frames. ACF scripts are intentionally
    /// not interpreted: the frames are decoded in stream order and can be
    /// played back with a palette chosen by the user.
    /// </summary>
    public class AnimationResourceViewModel : DocumentViewModel
    {
        private BmaAnimation _animation;
        private byte[] _animationData;
        private Project _project;
        private ResourceInfo _resourceInfo;

        private ImageSource _imageSource;
        private PaletteInfo _selectedPalette;
        private ObservableCollection<PaletteInfo> _availablePalettes;
        private int _currentFrameIndex;
        private double _scale = 1.0;
        private bool _isPlaying;
        private DispatcherTimer _timer;

        private readonly ICommand _playPauseCommand;
        private readonly ICommand _previousFrameCommand;
        private readonly ICommand _nextFrameCommand;
        private readonly ICommand _zoomInCommand;
        private readonly ICommand _zoomOutCommand;
        private readonly ICommand _resetZoomCommand;

        public AnimationResourceViewModel()
        {
            Title = "Animation";
            ToolTip = "BMA animation";
            CanClose = true;
            AvailablePalettes = new ObservableCollection<PaletteInfo>();

            _playPauseCommand = new RelayCommand(TogglePlayback);
            _previousFrameCommand = new RelayCommand(PreviousFrame);
            _nextFrameCommand = new RelayCommand(NextFrame);
            _zoomInCommand = new RelayCommand(ZoomIn);
            _zoomOutCommand = new RelayCommand(ZoomOut);
            _resetZoomCommand = new RelayCommand(ResetZoom);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(83) };
            _timer.Tick += (_, __) =>
            {
                if (FrameCount > 1)
                    CurrentFrameIndex = (CurrentFrameIndex + 1) % FrameCount;
            };
        }

        public ImageSource ImageSource
        {
            get => _imageSource;
            private set
            {
                _imageSource = value;
                OnPropertyChanged();
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

        public PaletteInfo SelectedPalette
        {
            get => _selectedPalette;
            set
            {
                if (ReferenceEquals(_selectedPalette, value)) return;
                _selectedPalette = value;
                OnPropertyChanged();
                RenderFrame();
            }
        }

        public int FrameCount => _animation?.Frames.Count ?? 0;
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
                RenderFrame();
            }
        }

        public string FrameLabel => FrameCount == 0
            ? "no frames"
            : $"{CurrentFrameIndex + 1} / {FrameCount}";

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
                RenderFrame();
            }
        }

        public int ScalePercentage => (int)Math.Round(_scale * 100);

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

        public void Initialize(Project project, ResourceInfo resourceInfo, byte[] animationData)
        {
            _project = project;
            _resourceInfo = resourceInfo;
            _animationData = animationData;

            LoadAvailablePalettes();
            LoadAnimation();

            if (AvailablePalettes.Count > 0)
                SelectedPalette = AvailablePalettes[0];
            else
                RenderFrame();
        }

        public override void Reload()
        {
            IsPlaying = false;
            LoadAnimation();
        }

        public override void Save()
        {
            // Animations are read-only in this viewer.
        }

        private void LoadAnimation()
        {
            if (_animationData == null || _animationData.Length == 0)
                return;

            try
            {
                _animation = BmaReader.Read(_animationData);
                _currentFrameIndex = 0;
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(LastFrameIndex));
                OnPropertyChanged(nameof(CurrentFrameIndex));
                OnPropertyChanged(nameof(FrameLabel));
                RenderFrame();
            }
            catch (Exception ex)
            {
                _animation = null;
                ImageSource = null;
                System.Diagnostics.Debug.WriteLine($"Failed to decode BMA animation: {ex.Message}");
            }
        }

        private void LoadAvailablePalettes()
        {
            AvailablePalettes.Clear();
            if (_project?.Files == null)
                return;

            ResourceFile container = _project.Files.FirstOrDefault(f =>
                f.Resources != null &&
                f.Resources.Any(r => r.Tag == _resourceInfo?.Tag && r.Number == _resourceInfo?.Number));

            if (container != null)
            {
                AddAcfPalette(container);
                AddPalettesFromFile(container);
            }

            // Fall back to all project palettes when the animation's GFF
            // does not carry its own palette (for example OBJEX.GFF).
            if (AvailablePalettes.Count == 0)
            {
                foreach (ResourceFile file in _project.Files)
                {
                    if (!ReferenceEquals(file, container))
                        AddPalettesFromFile(file);
                }
            }
        }

        private void AddPalettesFromFile(ResourceFile file)
        {
            if (file?.Resources == null || file.Data == null)
                return;

            try
            {
                GffFile gff = GffFile.Create(file.Data);
                foreach (ResourceInfo resource in file.Resources.Where(r => r.Type == ResourceType.Palette))
                {
                    byte[] paletteData = gff.GetResourceData(resource.Tag, resource.Number);
                    if (paletteData == null || paletteData.Length != 768)
                        continue;

                    AvailablePalettes.Add(new PaletteInfo
                    {
                        Palette = Palette.FromPalData(paletteData),
                        Name = $"{resource.Tag}-{resource.Number}",
                        Tag = resource.Tag,
                        Number = resource.Number
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load palettes from {file.Name}: {ex.Message}");
            }
        }

        private void AddAcfPalette(ResourceFile container)
        {
            if (container?.Resources == null || _resourceInfo == null)
                return;

            try
            {
                GffFile gff = GffFile.Create(container.Data);
                var acfResources = container.Resources
                    .Where(r => r.Tag == "ACF ")
                    .OrderBy(r => r.Number)
                    .ToList();
                if (acfResources.Count == 0)
                    return;

                int bmaId = _resourceInfo.Number;
                byte[] activePalette = null;
                AcfPaletteTimeline targetTimeline = null;

                Func<int, Palette> resolvePalette = paletteId =>
                {
                    ResourceInfo paletteResource = container.Resources
                        .FirstOrDefault(r => r.Tag == "PAL " && r.Number == paletteId);
                    if (paletteResource == null)
                        return null;

                    byte[] paletteData = gff.GetResourceData(paletteResource.Tag, paletteResource.Number);
                    return paletteData != null && paletteData.Length == 768
                        ? Palette.FromPalData(paletteData)
                        : null;
                };

                // Play the palette side of the intro in sequence. Most
                // cinematics inherit the palette left by the previous one;
                // BMA 10 in particular changes it on almost every frame.
                foreach (var acfResource in acfResources.Where(r => r.Number <= bmaId))
                {
                    AcfPaletteTimeline timeline = AcfPaletteTimelineReader.Read(
                        gff.GetResourceData(acfResource.Tag, acfResource.Number),
                        activePalette,
                        resolvePalette);

                    if (timeline.FinalPaletteData != null)
                        activePalette = timeline.FinalPaletteData;

                    if (acfResource.Number == bmaId)
                        targetTimeline = timeline;
                }

                if (targetTimeline == null)
                    return;

                if (targetTimeline.FramePalettes.Count > 0)
                {
                    AvailablePalettes.Add(new PaletteInfo
                    {
                        Palette = targetTimeline.FramePalettes[0],
                        FramePalettes = targetTimeline.FramePalettes,
                        Name = $"ACF {bmaId} (per-frame)",
                        Tag = "ACF ",
                        Number = bmaId
                    });
                }
                else if (targetTimeline.FinalPalette != null)
                {
                    string suffix = targetTimeline.PaletteChunkId is int paletteId
                        ? $"(PAL {paletteId})"
                        : "(inherited)";
                    AvailablePalettes.Add(new PaletteInfo
                    {
                        Palette = targetTimeline.FinalPalette,
                        Name = $"ACF {bmaId} {suffix}",
                        Tag = "ACF ",
                        Number = bmaId
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read ACF palette: {ex.Message}");
            }
        }

        private void RenderFrame()
        {
            if (_animation == null || _animation.Frames.Count == 0)
                return;

            try
            {
                ImageFrame frame = _animation.Frames[CurrentFrameIndex];
                byte[] pixels = frame.GetPixels();
                int width = frame.Width;
                int height = frame.Height;

                Palette activePalette = _selectedPalette?.Palette;
                if (_selectedPalette?.FramePalettes != null && _selectedPalette.FramePalettes.Count > 0)
                {
                    int paletteIndex = Math.Min(CurrentFrameIndex, _selectedPalette.FramePalettes.Count - 1);
                    activePalette = _selectedPalette.FramePalettes[paletteIndex];
                }

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

                        byte paletteIndex = pixels[originalY * width + originalX];
                        byte red, green, blue;
                        if (activePalette != null)
                        {
                            var color = activePalette.GetColor(paletteIndex);
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

                        int byteIndex = (y * scaledWidth + x) * 4;
                        pixelData[byteIndex] = blue;
                        pixelData[byteIndex + 1] = green;
                        pixelData[byteIndex + 2] = red;
                        pixelData[byteIndex + 3] = 255;
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
                System.Diagnostics.Debug.WriteLine($"Failed to render BMA frame: {ex.Message}");
            }
        }

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

        private void ZoomIn() => Scale *= 1.5;
        private void ZoomOut() => Scale /= 1.5;
        private void ResetZoom() => Scale = 1.0;
    }
}
