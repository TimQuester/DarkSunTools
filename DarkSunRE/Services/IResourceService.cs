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
    public interface IResourceService
    {
        ResourceType DetectResourceType(string tag, byte[] data);
        object DecodeResource(ResourceInfo resourceInfo, byte[] data);
        byte[] EncodeResource(ResourceInfo resourceInfo, object content);
        ImageSource CreateThumbnail(byte[] imageData, Palette palette);
        Task<ImageSource> CreateThumbnailAsync(byte[] imageData, Palette palette);
        string GetResourceDescription(string tag, int number);
        string GetFileExtension(ResourceType resourceType);

        bool CanEditResource(ResourceType resourceType);
        DocumentViewModel CreateEditorViewModel(ResourceInfo resourceInfo, byte[] data);
    }
}
