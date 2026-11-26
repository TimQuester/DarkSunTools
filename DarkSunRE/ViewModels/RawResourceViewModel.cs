using DarkSunRE.Models;
using System;

namespace DarkSunRE.ViewModels
{
    /// <summary>
    /// Metadata-only placeholder for UI, map, object, save and structural
    /// chunks that do not have a dedicated editor yet. It deliberately shows
    /// no raw bytes; the old hex viewer has been removed from the program.
    /// </summary>
    public sealed class RawResourceViewModel : DocumentViewModel
    {
        private string _resourceName = string.Empty;
        private string _kind = string.Empty;
        private string _group = string.Empty;
        private string _description = string.Empty;
        private string _sizeText = string.Empty;

        public string Message =>
            "No editor is available for this resource type yet.";

        public string ResourceName
        {
            get => _resourceName;
            private set { _resourceName = value; OnPropertyChanged(); }
        }

        public string Kind
        {
            get => _kind;
            private set { _kind = value; OnPropertyChanged(); }
        }

        public string Group
        {
            get => _group;
            private set { _group = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            private set { _description = value; OnPropertyChanged(); }
        }

        public string SizeText
        {
            get => _sizeText;
            private set { _sizeText = value; OnPropertyChanged(); }
        }

        public void Initialize(ResourceInfo resource, byte[] data)
        {
            byte[] bytes = data ?? Array.Empty<byte>();
            ResourceTagInfo classification = ResourceCatalog.Classify(resource?.Tag);
            ResourceGroupInfo group = ResourceCatalog.GetGroup(resource?.Group ?? classification.Group);

            ResourceName = resource == null ? "Resource" : $"{resource.Tag}-{resource.Number}";
            Kind = resource?.DisplayName ?? classification.DisplayName;
            Group = group.DisplayName;
            Description = resource?.Description ?? classification.Description;
            SizeText = $"{bytes.Length:N0} bytes, offset 0x{resource?.Offset ?? 0:X}";
            CanClose = true;
        }

        public override void Save()
        {
            // Read-only viewer.
        }

        public override void Reload()
        {
            // The parent document re-opens the resource to refresh it.
        }
    }
}
