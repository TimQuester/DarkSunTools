using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DSunTools.Common.Gff;
using DSunTools.Common.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DarkSunRE.Services
{
    /// <summary>
    /// One JSON file per GFF container for all WIND/BUTN records. Keeping one
    /// file per button produced hundreds of tiny files; ui.json is the single
    /// source for the whole interface.
    /// </summary>
    public static class InterfaceJsonStore
    {
        public const string LegacyFileName = "ui.json";

        public static string GetPath(Project project, ResourceFile container)
        {
            if (string.IsNullOrWhiteSpace(project?.JsonFolderPath) || container == null)
                return string.Empty;

            string stem = Path.GetFileNameWithoutExtension(container.Name);
            return Path.Combine(project.JsonFolderPath, $"{stem}_ui.json");
        }

        public static void Export(Project project, ResourceFile container)
        {
            string path = GetPath(project, container);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("JSON folder is not configured.");

            var gff = GffFile.Create(container.Data);
            var file = new InterfaceFileJson
            {
                Container = container.Name,
                Resources = new List<InterfaceResourceJson>()
            };

            foreach (ResourceInfo resource in container.Resources ?? new List<ResourceInfo>())
            {
                string tag = resource.Tag?.Trim();
                if (tag != "WIND" && tag != "BUTN")
                    continue;

                byte[] original = gff.GetResourceData(resource.Tag, resource.Number);
                byte[] current = resource.ModifiedData ?? original;
                file.Resources.Add(CaptureResource(resource, original, current));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static int Apply(Project project, ResourceFile container, string path = null)
        {
            path ??= GetPath(project, container);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return 0;

            InterfaceFileJson file = JsonSerializer.Deserialize<InterfaceFileJson>(File.ReadAllText(path));
            if (file?.Resources == null)
                return 0;

            var gff = GffFile.Create(container.Data);
            int applied = 0;
            foreach (InterfaceResourceJson entry in file.Resources)
            {
                if (entry == null)
                    continue;

                string tag = (entry.Tag ?? string.Empty).Trim();
                ResourceInfo resource = (container.Resources ?? new List<ResourceInfo>())
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.Tag?.Trim(), tag, StringComparison.Ordinal) &&
                        candidate.Number == entry.Number);
                if (resource == null)
                    continue;

                byte[] original = gff.GetResourceData(resource.Tag, resource.Number);
                byte[] current = resource.ModifiedData ?? original;
                byte[] updated = ApplyResource(entry, current);
                if (updated == null)
                    continue;

                resource.ModifiedData = updated;
                resource.IsModified = true;
                applied++;
            }

            return applied;
        }

        public static InterfaceResourceJson CaptureResource(
            ResourceInfo resource,
            byte[] original,
            byte[] current)
        {
            string tag = resource.Tag.Trim();
            var entry = new InterfaceResourceJson
            {
                Tag = tag,
                Number = resource.Number,
                Fields = new List<InterfaceFieldJson>(),
                Items = new List<InterfaceItemJson>()
            };

            if (tag == "WIND")
            {
                WindResource originalWindow = WindResource.Parse(original);
                WindResource currentWindow = WindResource.Parse(current);
                foreach (WindItem item in currentWindow.Items)
                {
                    WindItem originalItem = originalWindow.Items.FirstOrDefault(candidate =>
                        string.Equals(candidate.Tag, item.Tag, StringComparison.Ordinal) && candidate.Id == item.Id);
                    entry.Items.Add(new InterfaceItemJson
                    {
                        Tag = item.Tag,
                        Id = item.Id,
                        OriginalX = originalItem?.X ?? item.X,
                        OriginalY = originalItem?.Y ?? item.Y,
                        X = item.X,
                        Y = item.Y
                    });
                }
            }
            else if (tag == "BUTN")
            {
                ButnResource originalButton = ButnResource.Parse(original);
                ButnResource currentButton = ButnResource.Parse(current);
                AddField(entry, "Width", originalButton.Width, currentButton.Width);
                AddField(entry, "Height", originalButton.Height, currentButton.Height);
                AddField(entry, "IconX", originalButton.IconX, currentButton.IconX);
                AddField(entry, "IconY", originalButton.IconY, currentButton.IconY);
                AddField(entry, "TextX", originalButton.TextX, currentButton.TextX);
                AddField(entry, "TextY", originalButton.TextY, currentButton.TextY);
                AddField(entry, "IconId", originalButton.IconId, currentButton.IconId);
                AddField(entry, "Hotkey", originalButton.Hotkey, currentButton.Hotkey);
                AddField(entry, "Text", originalButton.Text, currentButton.Text);
            }

            return entry;
        }

        private static byte[] ApplyResource(InterfaceResourceJson entry, byte[] data)
        {
            string tag = (entry.Tag ?? string.Empty).Trim();
            if (tag == "WIND")
            {
                WindResource window = WindResource.Parse(data);
                foreach (InterfaceItemJson item in entry.Items ?? new List<InterfaceItemJson>())
                {
                    WindItem target = window.FindItem(item.Tag, item.Id);
                    if (target == null)
                        continue;
                    target.X = item.X;
                    target.Y = item.Y;
                }
                return window.Build();
            }

            if (tag == "BUTN")
            {
                ButnResource button = ButnResource.Parse(data);
                foreach (InterfaceFieldJson field in entry.Fields ?? new List<InterfaceFieldJson>())
                {
                    if (string.Equals(field.Name, "Text", StringComparison.OrdinalIgnoreCase))
                    {
                        button.Text = field.Value ?? string.Empty;
                        continue;
                    }

                    if (!int.TryParse(field.Value, out int value))
                        continue;
                    switch (field.Name)
                    {
                        case "Width": button.Width = value; break;
                        case "Height": button.Height = value; break;
                        case "IconX": button.IconX = value; break;
                        case "IconY": button.IconY = value; break;
                        case "TextX": button.TextX = value; break;
                        case "TextY": button.TextY = value; break;
                        case "IconId": button.IconId = (uint)value; break;
                        case "Hotkey": button.Hotkey = (byte)value; break;
                    }
                }
                return button.Build();
            }

            return null;
        }

        private static void AddField(InterfaceResourceJson entry, string name, object original, object current)
        {
            entry.Fields.Add(new InterfaceFieldJson
            {
                Name = name,
                OriginalValue = Convert.ToString(original, System.Globalization.CultureInfo.InvariantCulture),
                Value = Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture)
            });
        }
    }

    public sealed class InterfaceFileJson
    {
        public string Container { get; set; }
        public List<InterfaceResourceJson> Resources { get; set; } = new();
    }
}
