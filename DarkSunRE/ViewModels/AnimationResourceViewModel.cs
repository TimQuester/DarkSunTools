using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
using Microsoft.Win32;

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
        private readonly IStatusService _statusService;

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
        private readonly ICommand _exportPngCommand;
        private readonly ICommand _importPngCommand;

        public AnimationResourceViewModel(IStatusService statusService = null)
        {
            _statusService = statusService;
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
            _exportPngCommand = new RelayCommand(ExportPngFrames);
            _importPngCommand = new RelayCommand(ImportPngFrames);

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
        public ICommand ExportPngCommand => _exportPngCommand;
        public ICommand ImportPngCommand => _importPngCommand;

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

        private void ExportPngFrames()
        {
            if (_animation == null || _animation.Frames.Count == 0)
            {
                _statusService?.ShowError("There are no animation frames to export.");
                return;
            }

            var dialog = new OpenFolderDialog { Title = "Select folder for animation PNG frames" };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string baseName = GetFrameBaseName();
                for (int i = 0; i < _animation.Frames.Count; i++)
                {
                    ImageFrame frame = _animation.Frames[i];
                    string path = Path.Combine(dialog.FolderName, $"{baseName}_{i:D3}.png");
                    AnimationPngCodec.ExportFrame(
                        path,
                        frame.GetPixels(),
                        frame.Width,
                        frame.Height,
                        GetPaletteForFrame(i));
                }

                _statusService?.ShowSuccess(
                    $"Exported {_animation.Frames.Count} PNG frames to {dialog.FolderName}");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to export animation frames: {ex.Message}", ex);
            }
        }

        private void ImportPngFrames()
        {
            if (_resourceInfo == null)
            {
                _statusService?.ShowError(
                    "Open an existing BMA resource before importing; the import replaces that resource.");
                return;
            }

            var dialog = new OpenFolderDialog { Title = "Select folder with animation PNG frames" };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var allEntries = Directory.GetFiles(dialog.FolderName, "*.png")
                    .Select(ParseFrameFile)
                    .OfType<PngFrameFile>()
                    .ToList();

                if (allEntries.Count == 0)
                    throw new InvalidDataException(
                        "No PNG frames with a numeric suffix were found (expected name_000.png).");

                // A folder may contain exported frames for several BMA
                // resources (BMA-2_000.png, BMA-3_000.png, ...). Pick the
                // frames belonging to the animation currently open instead
                // of treating their identical frame numbers as duplicates.
                string targetBase = GetFrameBaseName();
                var entries = allEntries
                    .Where(entry => string.Equals(entry.BaseName, targetBase, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.Number)
                    .ToList();

                if (entries.Count == 0)
                {
                    var bases = allEntries
                        .Select(entry => entry.BaseName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (bases.Count > 1)
                    {
                        throw new InvalidDataException(
                            $"The folder contains frames for several animations " +
                            $"({string.Join(", ", bases.Take(6))}); expected {targetBase}_000.png.");
                    }

                    // A single differently-named animation is still allowed;
                    // the user may have renamed the exported files.
                    entries = allEntries.OrderBy(entry => entry.Number).ToList();
                }

                if (entries.GroupBy(entry => entry.Number).Any(group => group.Count() > 1))
                    throw new InvalidDataException(
                        $"The folder contains duplicate frame numbers for {targetBase}.");

                var imported = new List<AnimationPngCodec.ImportedFrame>();
                foreach (var entry in entries)
                    imported.Add(AnimationPngCodec.ImportFrame(entry.Path));

                AnimationPngCodec.ImportedFrame first = imported[0];
                int width = first.Width;
                int height = first.Height;

                if (_animation != null &&
                    (width != _animation.Width || height != _animation.Height))
                {
                    throw new InvalidDataException(
                        $"PNG dimensions are {width}x{height}, but this animation is " +
                        $"{_animation.Width}x{_animation.Height}.");
                }

                if (imported.Any(frame => frame.Width != width || frame.Height != height))
                    throw new InvalidDataException("All PNG frames must have the same dimensions.");

                if (imported.Any(frame => !PalettesEqual(frame.Palette, first.Palette)))
                {
                    _statusService?.ShowWarning(
                        "PNG frames use different palettes. BMA stores only palette indices; " +
                        "the game palette comes from the animation script/PAL chunks.");
                }

                if (_selectedPalette == null)
                {
                    throw new InvalidOperationException(
                        "Select the game palette (ACF or PAL) before importing; " +
                        "PNG indices are remapped through it.");
                }

                // The ACF segment table is the authoritative container
                // layout. Prefer it over the currently opened BMA: a file
                // generated by the old importer may already have one
                // container per frame while the matching ACF still has the
                // original 2/6/... segments.
                var acfSegments = GetAcfSegments();
                var containerLayout = acfSegments.Count > 0
                    ? acfSegments.Select(segment => segment.FrameCount).ToList()
                    : _animation?.Containers.Select(container => container.FrameCount).ToList();
                int expectedFrameCount = containerLayout?.Sum()
                    ?? _animation?.Frames.Count
                    ?? imported.Count;

                if (expectedFrameCount <= 0)
                    throw new InvalidDataException("The animation has no frames to import.");

                string frameCountNote = string.Empty;
                int suppliedFrameCount = imported.Count;
                if (imported.Count < expectedFrameCount)
                {
                    int missing = expectedFrameCount - imported.Count;
                    AnimationPngCodec.ImportedFrame last = imported[imported.Count - 1];
                    for (int i = 0; i < missing; i++)
                    {
                        imported.Add(new AnimationPngCodec.ImportedFrame
                        {
                            Width = last.Width,
                            Height = last.Height,
                            Pixels = last.Pixels,
                            Palette = last.Palette
                        });
                    }
                    frameCountNote = $" {missing} missing frame(s) were padded by repeating the last frame.";
                    _statusService?.ShowWarning(
                        $"Only {suppliedFrameCount} of {expectedFrameCount} PNG frames were found; " +
                        "the last frame was repeated to keep the ACF playback timeline valid.");
                }
                else if (imported.Count > expectedFrameCount)
                {
                    int ignored = imported.Count - expectedFrameCount;
                    imported.RemoveRange(expectedFrameCount, ignored);
                    frameCountNote = $" {ignored} extra frame(s) were ignored.";
                    _statusService?.ShowWarning(
                        $"The folder has {expectedFrameCount + ignored} PNG frames, but the ACF " +
                        $"timeline expects {expectedFrameCount}; {ignored} extra frame(s) were ignored.");
                }

                var framePixels = new List<byte[]>(imported.Count);
                int nearestPixels = 0;
                for (int i = 0; i < imported.Count; i++)
                {
                    Palette targetPalette = GetPaletteForFrame(i);
                    if (targetPalette == null)
                        throw new InvalidOperationException($"No palette is available for frame {i}.");

                    AnimationPngCodec.PaletteRemapResult remap =
                        AnimationPngCodec.RemapToPalette(imported[i], targetPalette);
                    framePixels.Add(remap.Pixels);
                    nearestPixels += remap.NearestPixels;
                }

                if (containerLayout != null && containerLayout.Sum() != imported.Count)
                {
                    throw new InvalidDataException(
                        $"The ACF container layout expects {containerLayout.Sum()} frames, " +
                        $"but {imported.Count} frames are being imported.");
                }

                byte[] bma = BmaWriter.Write(framePixels, width, height, containerLayout);
                BmaAnimation encoded = BmaReader.Read(bma);
                var segments = encoded.Containers
                    .Select((container, index) => new AcfSegmentInfo(index, container.FrameCount, container.Size))
                    .ToList();
                bool acfPatched = containerLayout != null && PatchAcfSegments(segments);

                _animationData = bma;
                if (_resourceInfo != null)
                {
                    _resourceInfo.ModifiedData = bma;
                    _resourceInfo.IsModified = true;
                }

                LoadAnimation();
                IsModified = true;

                if (nearestPixels > 0)
                {
                    _statusService?.ShowWarning(
                        $"{nearestPixels} pixel(s) had no exact RGB match in the selected palette; " +
                        "the nearest game colors were used. Check that the ACF/PAL palette matches the PNG.");
                }

                string targetName = $"{_resourceInfo.Tag.Trim()}-{_resourceInfo.Number}";
                string acfNote = acfPatched
                    ? " ACF segment table was rebuilt for the new containers."
                    : string.Empty;
                _statusService?.ShowSuccess(
                    $"Imported {suppliedFrameCount} PNG frames into {targetName} (replacing the existing BMA chunk). " +
                    "Palette indices were remapped from the PNG colors to the selected game palette." +
                    acfNote +
                    " Use Generate new file to write it into the GFF." +
                    frameCountNote);
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to import animation frames: {ex.Message}", ex);
            }
        }

        private IReadOnlyList<AcfSegmentInfo> GetAcfSegments()
        {
            if (_project?.Files == null || _resourceInfo == null)
                return Array.Empty<AcfSegmentInfo>();

            ResourceFile container = _project.Files.FirstOrDefault(file =>
                file.Resources != null &&
                file.Resources.Any(resource =>
                    resource.Tag == _resourceInfo.Tag && resource.Number == _resourceInfo.Number));
            if (container?.Resources == null || container.Data == null)
                return Array.Empty<AcfSegmentInfo>();

            ResourceInfo acfResource = container.Resources.FirstOrDefault(resource =>
                resource.Tag == "ACF " && resource.Number == _resourceInfo.Number);
            if (acfResource == null)
                return Array.Empty<AcfSegmentInfo>();

            GffFile gff = GffFile.Create(container.Data);
            byte[] acfData = acfResource.ModifiedData
                             ?? gff.GetResourceData(acfResource.Tag, acfResource.Number);
            return AcfSegmentPatcher.ReadSegments(acfData);
        }

        /// <summary>
        /// Keep the ACF op-01 segment byte sizes in sync with the rebuilt BMA.
        /// The engine seeks by these sizes; stale values stop playback after
        /// the first container or make it crash.
        /// </summary>
        private bool PatchAcfSegments(IReadOnlyList<AcfSegmentInfo> segments)
        {
            if (_project?.Files == null || _resourceInfo == null)
                return false;

            ResourceFile container = _project.Files.FirstOrDefault(file =>
                file.Resources != null &&
                file.Resources.Any(resource =>
                    resource.Tag == _resourceInfo.Tag && resource.Number == _resourceInfo.Number));
            if (container?.Resources == null || container.Data == null)
                return false;

            ResourceInfo acfResource = container.Resources.FirstOrDefault(resource =>
                resource.Tag == "ACF " && resource.Number == _resourceInfo.Number);
            if (acfResource == null)
                return false;

            GffFile gff = GffFile.Create(container.Data);
            byte[] acfData = acfResource.ModifiedData
                             ?? gff.GetResourceData(acfResource.Tag, acfResource.Number);
            byte[] patched = AcfSegmentPatcher.RebuildSegments(acfData, segments);

            acfResource.ModifiedData = patched;
            acfResource.IsModified = true;
            container.IsModified = true;
            return true;
        }

        private Palette GetPaletteForFrame(int frameIndex)
        {
            Palette palette = _selectedPalette?.Palette;
            if (_selectedPalette?.FramePalettes != null && _selectedPalette.FramePalettes.Count > 0)
            {
                int index = Math.Min(frameIndex, _selectedPalette.FramePalettes.Count - 1);
                palette = _selectedPalette.FramePalettes[index];
            }

            return palette;
        }

        private string GetFrameBaseName()
        {
            string raw = _resourceInfo == null
                ? "animation"
                : $"{_resourceInfo.Tag.Trim()}-{_resourceInfo.Number}";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(raw) ? "animation" : raw;
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

        private static bool PalettesEqual(
            IReadOnlyList<System.Windows.Media.Color> left,
            IReadOnlyList<System.Windows.Media.Color> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
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
