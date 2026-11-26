using DarkSunRE.Models;
using System;
using System.Text;

namespace DarkSunRE.ViewModels
{
    /// <summary>
    /// Read-only hex viewer used for UI, map, object, save and structural
    /// chunks that do not have a dedicated editor yet. Keeping them in a raw
    /// view is preferable to feeding them to the GPL parser, which produced
    /// misleading "strings" for arbitrary binary data.
    /// </summary>
    public sealed class RawResourceViewModel : DocumentViewModel
    {
        private string _resourceName = string.Empty;
        private string _kind = string.Empty;
        private string _group = string.Empty;
        private string _description = string.Empty;
        private string _sizeText = string.Empty;
        private string _hexDump = string.Empty;

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

        public string HexDump
        {
            get => _hexDump;
            private set { _hexDump = value; OnPropertyChanged(); }
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
            HexDump = BuildHexDump(bytes);
            CanClose = true;
        }

        private static string BuildHexDump(byte[] bytes)
        {
            if (bytes.Length == 0)
                return "(empty resource)";

            var text = new StringBuilder(bytes.Length * 4);
            for (int offset = 0; offset < bytes.Length; offset += 16)
            {
                int count = Math.Min(16, bytes.Length - offset);
                text.Append(offset.ToString("X8")).Append("  ");

                for (int i = 0; i < 16; i++)
                {
                    if (i == 8)
                        text.Append(' ');
                    text.Append(i < count ? bytes[offset + i].ToString("X2") : "  ");
                    text.Append(' ');
                }

                text.Append(" |");
                for (int i = 0; i < count; i++)
                {
                    byte value = bytes[offset + i];
                    text.Append(value >= 0x20 && value <= 0x7E ? (char)value : '.');
                }
                text.AppendLine("|");
            }

            return text.ToString();
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
