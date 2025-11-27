using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DSunTools.Common.Image;
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
            throw new NotImplementedException();
        }

        public DocumentViewModel CreateEditorViewModel(ResourceInfo resourceInfo, byte[] data)
        {
            throw new NotImplementedException();
        }

        //private readonly IPaletteService _paletteService;
        //private readonly IImageDecoder _imageDecoder;

        //public ResourceService(IPaletteService paletteService, IImageDecoder imageDecoder)
        //{
        //    _paletteService = paletteService;
        //    _imageDecoder = imageDecoder;
        //}

        public ImageSource CreateThumbnail(byte[] imageData, Palette palette)
        {
            throw new NotImplementedException();
        }

        public Task<ImageSource> CreateThumbnailAsync(byte[] imageData, Palette palette)
        {
            throw new NotImplementedException();
        }

        public object DecodeResource(ResourceInfo resourceInfo, byte[] data)
        {
            throw new NotImplementedException();
        }

        public ResourceType DetectResourceType(string tag, byte[] data)
        {
            // Реализация
            throw new NotImplementedException();
        }

        public byte[] EncodeResource(ResourceInfo resourceInfo, object content)
        {
            throw new NotImplementedException();
        }

        public string GetFileExtension(ResourceType resourceType)
        {
            throw new NotImplementedException();
        }

        public string GetResourceDescription(string tag, int number)
        {
            throw new NotImplementedException();
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
            //return DetectBySignature(data);
            return ResourceType.Unknown;
        }
        private ResourceType DetectByTag(string tag)
        {
            return tag switch
            {
                "BMP " or "TGA " or
                "SPR " or "ICON" or
                "APFM" or "BUTN" or
                "EBOX" or "ACCL" => ResourceType.Image,
                "PAL " or "CPAL" => ResourceType.Palette,
                "FNT " => ResourceType.Font,
                "TEXT" or "MSG " or "NAME" => ResourceType.Text,
                "GPL " => ResourceType.Script,
                "WAV " or "VOC " or
                "BVOC" => ResourceType.Sound,
                "ITM " => ResourceType.Item,
                "CHR " => ResourceType.Character,
                "RMAP" or "GMAP" => ResourceType.Map,
                "TILE" => ResourceType.Tileset,
                "ANM " => ResourceType.Animation,
                "ETAB" => ResourceType.Item,
                "MAS " or "WALL" or 
                "PORT" or "IT1R" or 
                "GPLX" or "GPLI" or
                "GFFI" or "WIND" or
                "ADV " or "CMAT" => ResourceType.Unknown,
                _ => ResourceType.Unknown
            };
        }

    }
}
