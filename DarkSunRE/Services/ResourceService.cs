using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DSunTools.Common.Image;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Services
{
    public class ResourceService : IResourceService
    {
        public bool CanEditResource(ResourceType resourceType)
        {
            // Определяем, какие типы ресурсов можно редактировать
            return resourceType switch
            {
                ResourceType.Text => true,
                ResourceType.Script => true,
                ResourceType.Image => true,
                ResourceType.Palette => true,
                ResourceType.Font => true,
                ResourceType.RawData => false, // RawData редактируется как есть
                _ => false
            };
        }

        public DocumentViewModel CreateEditorViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            return resourceInfo.Type switch
            {
                ResourceType.Script => CreateGplDocumentViewModel(resourceInfo, data),
                ResourceType.Text => CreateTextResourceViewModel(resourceInfo, data),
                ResourceType.Image => CreateImageResourceViewModel(resourceInfo, data),
                ResourceType.Palette => CreatePaletteViewModel(resourceInfo, data),
                ResourceType.Font => CreateFontResourceViewModel(resourceInfo, data),
                _ => CreateRawDataViewModel(resourceInfo, data)
            };
        }

        //private readonly IPaletteService _paletteService;
        //private readonly IImageDecoder _imageDecoder;

        //public ResourceService(IPaletteService paletteService, IImageDecoder imageDecoder)
        //{
        //    _paletteService = paletteService;
        //    _imageDecoder = imageDecoder;
        //}

        public string GetResourceDescription(string tag, int number)
        {
            return $"{tag}-{number}";
        }

        public string GetFileExtension(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Image => ".bmp",
                ResourceType.Palette => ".pal",
                ResourceType.Font => ".fnt",
                ResourceType.Text => ".txt",
                ResourceType.Script => ".gpl",
                ResourceType.Sound => ".wav",
                ResourceType.Music => ".mid",
                ResourceType.Dialog => ".dlg",
                ResourceType.Item => ".itm",
                ResourceType.Character => ".chr",
                ResourceType.Map => ".map",
                ResourceType.Animation => ".anm",
                ResourceType.RawData => ".dat",
                ResourceType.AnimationControl => ".acf",
                _ => ".dat"
            };
        }

        public ImageSource CreateThumbnail(byte[] imageData, Palette palette)
        {
            try
            {
                // Создаем миниатюру изображения
                var frames = ImageReading.ExtractFrames(imageData);
                if (frames.Count > 0)
                {
                    // Создаем миниатюру из первого кадра
                    var frame = frames[0];
                    var pixels = frame.GetPixels();
                    var alphaMask = frame.GetAlphaMask();

                    // Создаем WriteableBitmap для миниатюры
                    int width = frame.Width;
                    int height = frame.Height;

                    // Ограничиваем размер миниатюры
                    int thumbWidth = Math.Min(width, 64);
                    int thumbHeight = Math.Min(height, 64);

                    // Создаем миниатюру (пока возвращаем null, так как требует дополнительной реализации)
                    return null;
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустую миниатюру
            }

            return null;
        }

        public async Task<ImageSource> CreateThumbnailAsync(byte[] imageData, Palette palette)
        {
            // Асинхронная версия создания миниатюры
            return await Task.Run(() => CreateThumbnail(imageData, palette));
        }

        public object DecodeResource(ResourceInfo resourceInfo, byte[] data)
        {
            return resourceInfo.Type switch
            {
                ResourceType.Script => DecodeGplResource(data),
                ResourceType.Image => DecodeImageResource(data),
                ResourceType.Palette => DecodePaletteResource(data),
                ResourceType.Font => DecodeFontResource(data),
                ResourceType.Text => DecodeTextResource(data),
                _ => data // Для неизвестных типов возвращаем сырые данные
            };
        }
        public byte[] EncodeResource(ResourceInfo resourceInfo, object content)
        {
            return resourceInfo.Type switch
            {
                ResourceType.Script => EncodeGplResource(content),
                ResourceType.Image => EncodeImageResource(content),
                ResourceType.Palette => EncodePaletteResource(content),
                ResourceType.Font => EncodeFontResource(content),
                ResourceType.Text => EncodeTextResource(content),
                _ => content as byte[] ?? Array.Empty<byte>()
            };
        }

        public ResourceType DetectResourceType(string tag, byte[] data)
        {
            if (data == null || data.Length == 0)
                return ResourceType.Unknown;

            // Определяем по тегу
            var typeByTag = DetectByTag(tag);
            if (typeByTag != ResourceType.Unknown)
                return typeByTag;

            // Определяем по сигнатуре данных
            return DetectBySignature(data);
        }

        ResourceType IResourceService.DetectResourceType(string tag, byte[] data)
        {
            if (data == null || data.Length == 0)
                return ResourceType.Unknown;

            // Определяем по тегу
            var typeByTag = DetectByTag(tag);
            if (typeByTag != ResourceType.Unknown)
                return typeByTag;

            // Определяем по сигнатуре данных
            return DetectBySignature(data);            
        }
        private ResourceType DetectByTag(string tag)
        {
            return tag switch
            {
                "BMP " or "TGA " or
                "SPR " or "ICON" or
                "BUTN" or "WALL" or
                "PORT" or "CMAT" or
                "TILE" or "CBMP" => ResourceType.Image,
                "PAL " or "CPAL" => ResourceType.Palette,
                "FNT " => ResourceType.Font,
                "TEXT" or "MSG " or
                "NAME" or "MERR" or
                "ETME" or "SPIN" or
                "RNME" => ResourceType.Text,
                "GPL " => ResourceType.Script,
                "WAV " or "VOC " or
                "BVOC" => ResourceType.Sound,
                "ITM " => ResourceType.Item,
                "CHR " => ResourceType.Character,
                "RMAP" or "GMAP" => ResourceType.Map,
                "ANM " or "BMA " => ResourceType.Animation,
                "ACF " => ResourceType.AnimationControl,                
                "PSEQ" or "CSEQ" or
                "LSEQ" or "GSEQ" or
                "FSEQ" => ResourceType.Music,
                "ETAB" => ResourceType.Item,
                "MAS " or "IT1R" or
                "EBOX" or "ACCL" or
                "GPLX" or "GPLI" or
                "GFFI" or "WIND" or
                "ADV " or "APFM" => ResourceType.Unknown,
                _ => ResourceType.Unknown
            };
        }
        private ResourceType DetectBySignature(byte[] data)
        {
            if (data == null || data.Length < 4)
                return ResourceType.Unknown;

            // Проверяем сигнатуры файлов
            // GPL файлы начинаются с определенных байтов
            if (data.Length > 10 && data[0] == 0x05)
            {
                // Проверяем, есть ли характерные для GPL данные
                // GPL файлы содержат маркеры 0x05 и заканчиваются 0x03
                return ResourceType.Script;
            }

            // Проверяем сигнатуры изображений
            if (data.Length >= 4)
            {
                // TGA файлы
                if (data[2] == 0x02 || data[2] == 0x0A)
                    return ResourceType.Image;

                // BMP файлы
                if (data[0] == 0x42 && data[1] == 0x4D)
                    return ResourceType.Image;
            }

            // Проверяем сигнатуры палитр
            if (data.Length == 768) // PAL файлы обычно 768 байт (256 цветов * 3 байта)
            {
                return ResourceType.Palette;
            }

            // Проверяем сигнатуры шрифтов
            if (data.Length > 10)
            {
                // FNT файлы имеют определенную структуру
                // Пока возвращаем Unknown для неизвестных типов
            }

            return ResourceType.Unknown;
        }

        private object DecodeGplResource(byte[] data)
        {
            // GPL ресурсы уже обрабатываются в GPLDocument
            // Возвращаем сырые данные, которые будут обработаны в GPLDocument
            return data;
        }

        private object DecodeImageResource(byte[] data)
        {
            // Для изображений возвращаем сырые данные, которые будут обработаны позже
            try
            {
                // Пытаемся определить тип изображения
                if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
                {
                    // BMP файл
                    return data;
                }
                else if (data.Length >= 3 && data[2] == 0x02)
                {
                    // TGA файл
                    return data;
                }
                else
                {
                    // Неизвестный формат изображения
                    return data;
                }
            }
            catch
            {
                // В случае ошибки возвращаем сырые данные
                return data;
            }
        }
        private object DecodePaletteResource(byte[] data)
        {
            try
            {
                // Палитры обычно 768 байт (256 цветов * 3 байта)
                if (data.Length == 768)
                {
                    return Palette.FromPalData(data);
                }
                else
                {
                    // Если размер не соответствует ожидаемому, возвращаем сырые данные
                    return data;
                }
            }
            catch
            {
                // В случае ошибки возвращаем сырые данные
                return data;
            }
        }

        private byte[] EncodeGplResource(object content)
        {
            // GPL ресурсы кодируются через GPLDocument
            if (content is GPLDocument gplDoc)
            {
                gplDoc.ApplyStringChanges();
                return gplDoc.newBytes;
            }
            else if (content is byte[] bytes)
            {
                // Если контент уже в нужном формате
                return bytes;
            }
            return content as byte[] ?? Array.Empty<byte>();
        }

        private byte[] EncodeImageResource(object content)
        {
            // Для изображений возвращаем сырые данные
            return content as byte[] ?? Array.Empty<byte>();
        }

        private byte[] EncodePaletteResource(object content)
        {
            // Для палитр возвращаем сырые данные
            // Палитры обычно не редактируются напрямую
            return content as byte[] ?? Array.Empty<byte>();
        }

        private byte[] EncodeFontResource(object content)
        {
            // Для шрифтов возвращаем сырые данные
            return content as byte[] ?? Array.Empty<byte>();
        }

        private byte[] EncodeTextResource(object content)
        {
            if (content is string text)
            {
                try
                {
                    return Encoding.ASCII.GetBytes(text);
                }
                catch
                {
                    return Array.Empty<byte>();
                }
            }
            return content as byte[] ?? Array.Empty<byte>();
        }

        private object DecodeTextResource(byte[] data)
        {
            try
            {
                // Пытаемся декодировать как ASCII текст
                return Encoding.ASCII.GetString(data);
            }
            catch
            {
                // В случае ошибки возвращаем сырые данные
                return data;
            }
        }

        private object DecodeFontResource(byte[] data)
        {
            // Для шрифтов возвращаем сырые данные
            // Позже они будут обработаны специализированным редактором
            return data;
        }

        #region CreateViewModels
        private DocumentViewModel CreateGplDocumentViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            var document = new GPLDocument(data, $"{resourceInfo.Tag}-{resourceInfo.Number}");
            return new GplDocumentViewModel(document);
        }

        private DocumentViewModel CreateTextResourceViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            // Реализация для текстовых ресурсов
            return new TextResourceViewModel();
        }

        private DocumentViewModel CreateImageResourceViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            // Реализация для изображений
            var viewModel = new ImageResourceViewModel();
            viewModel.ImageData = data;
            viewModel.Title = $"{resourceInfo.Tag}-{resourceInfo.Number}";
            viewModel.File = $"{resourceInfo.Tag}-{resourceInfo.Number}";
            return viewModel;
        }

        private DocumentViewModel CreatePaletteViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            // Реализация для палитр
            return new PaletteViewModel();
        }

        private DocumentViewModel CreateFontResourceViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            // Реализация для шрифтов
            return new FontResourceViewModel();
        }

        private DocumentViewModel CreateRawDataViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            // Реализация для неизвестных типов
            return new RawDataViewModel();
        } 
        #endregion
        
        // Методы для работы с GFF файлами
        public GffFile ParseGffFile(byte[] gffBytes)
        {
            return GffFile.Create(gffBytes);
        }
        
        public byte[] GetResourceData(GffFile gffFile, string tag, int resourceNumber)
        {
            return gffFile.GetResourceData(tag, resourceNumber);
        }
        
        public byte[] ReplaceResource(GffFile gffFile, string tag, int resourceNumber, byte[] replacement)
        {
            return gffFile.ReplaceResource(tag, resourceNumber, replacement);
        }
        
        public List<ResourceDescriptor> DescribeResources(GffFile gffFile)
        {
            return gffFile.DescribeResources();
        }
    }
}
