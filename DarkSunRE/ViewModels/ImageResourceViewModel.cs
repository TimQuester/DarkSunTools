using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSunTools.Common.Image;

namespace DarkSunRE.ViewModels
{
    public class ImageResourceViewModel : DocumentViewModel
    {
        private ImageSource _imageSource;
        private byte[] _imageData;
        private string _imagePath;

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

        public ImageResourceViewModel()
        {
            Title = "Image";
            ToolTip = "Image Resource";
            CanClose = true;
        }

        public override void Reload()
        {
            LoadImage();
        }

        public override void Save()
        {
            // Для изображений сохранение не требуется
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

                    // Создаем массив пикселей для BitmapSource
                    var pixelData = new byte[width * height * 4]; // 4 байта на пиксель (BGRA)

                    // Заполняем пиксели
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int pixelIndex = y * width + x;
                            int byteIndex = pixelIndex * 4; // 4 байта на пиксель (BGRA)

                            // Получаем индекс пикселя из данных изображения
                            byte paletteIndex = pixels[pixelIndex];

                            // Для простоты используем фиксированную палитру
                            // В реальной реализации нужно получать палитру из ресурсов
                            byte red = (byte)(paletteIndex * 4);
                            byte green = (byte)(paletteIndex * 2);
                            byte blue = (byte)(paletteIndex * 1);
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
                        width,
                        height,
                        96, // DPI X
                        96, // DPI Y
                        PixelFormats.Bgra32,
                        null, // палитра
                        pixelData,
                        width * 4 // шаг строки
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
}
