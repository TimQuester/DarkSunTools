using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DSunTools.Common.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using DSunTools.Common.Gff;

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
        
        // Методы для работы с GFF файлами
        GffFile ParseGffFile(byte[] gffBytes);
        byte[] GetResourceData(GffFile gffFile, string tag, int resourceNumber);
        byte[] ReplaceResource(GffFile gffFile, string tag, int resourceNumber, byte[] replacement);
        List<ResourceDescriptor> DescribeResources(GffFile gffFile);
    }
}
