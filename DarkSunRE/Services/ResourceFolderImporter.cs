using DarkSunRE.Models;
using DarkSunRE.ViewModels;
using DSunTools.Common.Gff;
using DSunTools.Common.Image;
using DSunTools.Common.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DarkSunRE.Services
{
    public sealed class ResourceFolderImportResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Looks for asset folders named after the GFF file (for example
    /// "Resource" for RESOURCE.GFF or "Cine" for CINE.GFF) under the modified
    /// game path and the JSON folder. Supported names:
    ///   TAG-N.png / TAG-N_000.png, TAG-N.txt, TAG-N.json
    /// The import happens before Generate new file builds the GFF.
    /// </summary>
    public static class ResourceFolderImporter
    {
        public static ResourceFolderImportResult Import(
            Project project,
            ResourceFile file,
            GffFile gff)
        {
            var result = new ResourceFolderImportResult();
            if (project == null || file?.Resources == null)
                return result;

            string combinedUiJson = InterfaceJsonStore.GetPath(project, file);
            if (!string.IsNullOrWhiteSpace(combinedUiJson) && File.Exists(combinedUiJson))
            {
                try
                {
                    result.Imported += InterfaceJsonStore.Apply(project, file, combinedUiJson);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{Path.GetFileName(combinedUiJson)}: {ex.Message}");
                }
            }

            foreach (string folder in GetFolders(project, file.Name))
            {
                // Accept the old folder-based ui.json files as a migration aid.
                string legacyUiJson = Path.Combine(folder, InterfaceJsonStore.LegacyFileName);
                if (File.Exists(legacyUiJson))
                {
                    try
                    {
                        result.Imported += InterfaceJsonStore.Apply(project, file, legacyUiJson);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{InterfaceJsonStore.LegacyFileName}: {ex.Message}");
                    }
                }

                foreach (ResourceInfo resource in file.Resources)
                {
                    string prefix = $"{resource.Tag.Trim()}-{resource.Number}";
                    var candidates = Directory.GetFiles(folder)
                        .Where(path =>
                        {
                            string name = Path.GetFileNameWithoutExtension(path);
                            return string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase) ||
                                   name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();
                    if (candidates.Count == 0)
                        continue;

                    try
                    {
                        if (resource.Type == ResourceType.Image &&
                            candidates.Any(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)))
                        {
                            byte[] original = gff.GetResourceData(resource.Tag, resource.Number);
                            byte[] current = resource.ModifiedData ?? original;
                            byte[] imported = ImportImage(project, file, resource, current, candidates);
                            if (imported != null)
                            {
                                resource.ModifiedData = imported;
                                resource.IsModified = true;
                                result.Imported++;
                            }
                            else
                            {
                                result.Skipped++;
                            }
                        }
                        else if (resource.Type == ResourceType.Text &&
                                 candidates.Any(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
                        {
                            string path = candidates.First(candidate =>
                                string.Equals(Path.GetExtension(candidate), ".txt", StringComparison.OrdinalIgnoreCase));
                            resource.ModifiedData = Encoding.Latin1.GetBytes(File.ReadAllText(path, Encoding.Latin1));
                            resource.IsModified = true;
                            result.Imported++;
                        }
                        else if (candidates.Any(path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)))
                        {
                            string path = candidates.First(candidate =>
                                string.Equals(Path.GetExtension(candidate), ".json", StringComparison.OrdinalIgnoreCase));
                            if (ImportInterfaceJson(gff, resource, path))
                            {
                                resource.IsModified = true;
                                result.Imported++;
                            }
                            else
                            {
                                result.Skipped++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{resource.Tag.Trim()}-{resource.Number}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private static IEnumerable<string> GetFolders(Project project, string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { project.ModifiedGamePath, project.JsonFolderPath, project.OriginalGamePath })
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;
                string folder = Path.Combine(root, stem);
                if (Directory.Exists(folder) && seen.Add(folder))
                    yield return folder;
            }
        }

        private static byte[] ImportImage(
            Project project,
            ResourceFile file,
            ResourceInfo resource,
            byte[] current,
            IReadOnlyList<string> candidates)
        {
            string prefix = $"{resource.Tag.Trim()}-{resource.Number}";
            var pngs = candidates
                .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                .Select(path => new
                {
                    Path = path,
                    Number = ParseFrameNumber(Path.GetFileNameWithoutExtension(path), prefix)
                })
                .OrderBy(entry => entry.Number)
                .ToList();

            if (pngs.Count == 0)
                return null;

            Palette palette = FindPalette(file);
            if (palette == null)
                throw new InvalidDataException("No PAL palette is available for automatic image import.");

            var imported = pngs.Select(entry => AnimationPngCodec.ImportFrame(entry.Path)).ToList();
            int width = imported[0].Width;
            int height = imported[0].Height;
            if (imported.Any(frame => frame.Width != width || frame.Height != height))
                throw new InvalidDataException("Imported PNG frames have different dimensions.");

            var pixels = new List<byte[]>();
            var masks = new List<System.Collections.BitArray?>();
            foreach (var frame in imported)
            {
                var remap = AnimationPngCodec.RemapToPalette(frame, palette);
                pixels.Add(remap.Pixels);
                masks.Add(frame.AlphaMask);
            }

            bool bma = resource.Tag.Trim() == "CMAT" || file.Resources.Any(candidate =>
                candidate.Tag == "BMA " || candidate.Tag == "ACF ");
            byte[] encoded;
            if (bma)
            {
                BmaAnimation currentAnimation = BmaReader.Read(current);
                if (width != currentAnimation.Width || height != currentAnimation.Height)
                    throw new InvalidDataException("BMA/CMAT frame dimensions cannot be changed by an image import.");
                encoded = BmaWriter.Write(
                    pixels,
                    width,
                    height,
                    currentAnimation.Containers.Select(container => container.FrameCount).ToList());
            }
            else
            {
                encoded = BitmapContainerWriter.Write(pixels, width, height, masks);
                if (resource.Tag.Trim() == "ICON")
                {
                    // old dimensions come from decoding the current chunk
                    var oldFrames = ImageReading.ExtractFrames(current);
                    ButtonSizeSyncService.ApplyIconSizeChange(
                        project,
                        file,
                        resource,
                        oldFrames[0].Width,
                        oldFrames[0].Height,
                        width,
                        height);
                }
            }

            return encoded;
        }

        private static Palette FindPalette(ResourceFile file)
        {
            ResourceInfo paletteResource = file.Resources.FirstOrDefault(resource =>
                resource.Tag == "PAL " && resource.Number == 1000)
                ?? file.Resources.FirstOrDefault(resource => resource.Tag == "PAL ");
            if (paletteResource == null)
                return null;

            var gff = GffFile.Create(file.Data);
            byte[] data = gff.GetResourceData(paletteResource.Tag, paletteResource.Number);
            return data.Length == 768 ? Palette.FromPalData(data) : null;
        }

        private static bool ImportInterfaceJson(GffFile gff, ResourceInfo resource, string path)
        {
            InterfaceResourceJson dto = JsonSerializer.Deserialize<InterfaceResourceJson>(File.ReadAllText(path));
            if (dto == null)
                return false;

            byte[] data = resource.ModifiedData ?? gff.GetResourceData(resource.Tag, resource.Number);
            if (string.Equals(resource.Tag.Trim(), "WIND", StringComparison.Ordinal))
            {
                WindResource window = WindResource.Parse(data);
                foreach (InterfaceItemJson item in dto.Items ?? new())
                {
                    WindItem target = window.FindItem(item.Tag, item.Id);
                    if (target != null)
                    {
                        target.X = item.X;
                        target.Y = item.Y;
                    }
                }
                resource.ModifiedData = window.Build();
                return true;
            }

            if (string.Equals(resource.Tag.Trim(), "BUTN", StringComparison.Ordinal))
            {
                ButnResource button = ButnResource.Parse(data);
                foreach (InterfaceFieldJson field in dto.Fields ?? new())
                {
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
                InterfaceFieldJson text = dto.Fields?.FirstOrDefault(field =>
                    string.Equals(field.Name, "Text", StringComparison.OrdinalIgnoreCase));
                if (text != null)
                    button.Text = text.Value ?? string.Empty;
                resource.ModifiedData = button.Build();
                return true;
            }

            return false;
        }

        private static int ParseFrameNumber(string name, string prefix)
        {
            string suffix = name.Length > prefix.Length ? name.Substring(prefix.Length) : string.Empty;
            Match match = Regex.Match(suffix, @"^_(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}
