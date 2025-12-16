using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSunTools.Common.Image;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System.Collections.ObjectModel;
using DSunTools.Common.Gff;
using System.Windows.Input;
using DarkSunRE.Commands;

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
        private ICommand _zoomInCommand;
        private ICommand _zoomOutCommand;
        private ICommand _resetZoomCommand;

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
                LoadImage();
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
                _selectedPalette = value;
                OnPropertyChanged();
                LoadImage(); // Перезагружаем изображение с новой палитрой
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
            }
        }

        public int ScalePercentage => (int)(_scale * 100);

        public ICommand ZoomInCommand => _zoomInCommand ?? (_zoomInCommand = new RelayCommand(ZoomIn));
        public ICommand ZoomOutCommand => _zoomOutCommand ?? (_zoomOutCommand = new RelayCommand(ZoomOut));
        public ICommand ResetZoomCommand => _resetZoomCommand ?? (_resetZoomCommand = new RelayCommand(ResetZoom));

        public ImageResourceViewModel()
        {
            Title = "Image";
            ToolTip = "Image Resource";
            CanClose = true;
            AvailablePalettes = new ObservableCollection<PaletteInfo>();
            _zoomInCommand = new RelayCommand(ZoomIn);
            _zoomOutCommand = new RelayCommand(ZoomOut);
            _resetZoomCommand = new RelayCommand(ResetZoom);
        }

        public void Initialize(Project project, ResourceInfo resourceInfo, byte[] imageData)
        {
            _project = project;
            _resourceInfo = resourceInfo;
            ImageData = imageData;
            
            // Загружаем доступные палитры из проекта
            LoadAvailablePalettes();
            
            // Устанавливаем первую палитру по умолчанию
            if (AvailablePalettes.Count > 0)
            {
                SelectedPalette = AvailablePalettes[0];
            }
        }

        public override void Reload()
        {
            LoadImage();
        }

        public override void Save()
        {
            // Для изображений сохранение не требуется
        }

        private void ZoomIn()
        {
            if (_scale < 10.0)
            {
                Scale = Math.Min(_scale * 1.5, 10.0);
                LoadImage();
            }
        }

        private void ZoomOut()
        {
            if (_scale > 0.1)
            {
                Scale = Math.Max(_scale / 1.5, 0.1);
                LoadImage();
            }
        }

        private void ResetZoom()
        {
            Scale = 1.0;
            LoadImage();
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
                            
                            if (paletteData != null && paletteData.Length == 768) // PAL файлы обычно 768 байт
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

        private void LoadImage()
        {
            if (_imageData == null || _imageData.Length == 0)
                return;

            try
            {
                // Извлекаем кадры изображения
                var frames = ImageReading.ExtractFrames(_imageData);
                if (frames.Count > 0)
                {
                    // Используем первый кадр
                    var frame = frames[0];
                    var pixels = frame.GetPixels();
                    var alphaMask = frame.GetAlphaMask();
                    int width = frame.Width;
                    int height = frame.Height;

                    // Если масштаб не 1.0, создаем увеличенное изображение
                    int scaledWidth = (int)(width * _scale);
                    int scaledHeight = (int)(height * _scale);

                    // Создаем массив пикселей для BitmapSource
                    var pixelData = new byte[scaledWidth * scaledHeight * 4]; // 4 байта на пиксель (BGRA)

                    // Заполняем пиксели с масштабированием
                    for (int y = 0; y < scaledHeight; y++)
                    {
                        for (int x = 0; x < scaledWidth; x++)
                        {
                            // Определяем исходный пиксель
                            int originalX = (int)(x / _scale);
                            int originalY = (int)(y / _scale);
                            
                            // Проверяем границы
                            if (originalX >= width || originalY >= height)
                                continue;

                            int pixelIndex = originalY * width + originalX;
                            int scaledPixelIndex = y * scaledWidth + x;
                            int byteIndex = scaledPixelIndex * 4; // 4 байта на пиксель (BGRA)

                            // Получаем индекс пикселя из данных изображения
                            byte paletteIndex = pixels[pixelIndex];

                            // Используем выбранную палитру или фиксированную палитру по умолчанию
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
                                // Фиксированная палитра по умолчанию
                                red = (byte)(paletteIndex * 4);
                                green = (byte)(paletteIndex * 2);
                                blue = (byte)(paletteIndex * 1);
                            }
                            
                            byte alpha = alphaMask.Get(pixelIndex) ? (byte)255 : (byte)0;

                            // Записываем пиксель в массив (BGRA формат)
                            pixelData[byteIndex] = blue;     // B
                            pixelData[byteIndex + 1] = green; // G
                            pixelData[byteIndex + 2] = red;   // R
                            pixelData[byteIndex + 3] = alpha; // A
                        }
                    }

                    // Создаем BitmapSource из массива пикселей
                    var bitmap = BitmapSource.Create(
                        scaledWidth,
                        scaledHeight,
                        96, // DPI X
                        96, // DPI Y
                        PixelFormats.Bgra32,
                        null, // палитра
                        pixelData,
                        scaledWidth * 4 // шаг строки
                    );

                    ImageSource = bitmap;
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки создаем пустое изображение
                ImageSource = null;
            }
        }
    }

    public class PaletteInfo
    {
        public Palette Palette { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public int Number { get; set; }
    }
}
