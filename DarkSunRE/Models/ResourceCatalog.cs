using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkSunRE.Models
{
    public sealed class ResourceTagInfo
    {
        public ResourceTagInfo(
            string tag,
            string displayName,
            ResourceGroup group,
            ResourceType type,
            string description)
        {
            Tag = tag;
            DisplayName = displayName;
            Group = group;
            Type = type;
            Description = description;
        }

        public string Tag { get; }
        public string DisplayName { get; }
        public ResourceGroup Group { get; }
        public ResourceType Type { get; }
        public string Description { get; }
        public bool IsKnown => Type != ResourceType.Unknown;
    }

    public sealed class ResourceGroupInfo
    {
        public ResourceGroupInfo(ResourceGroup group, string displayName, int order, string description)
        {
            Group = group;
            DisplayName = displayName;
            Order = order;
            Description = description;
        }

        public ResourceGroup Group { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Single source of truth for the GFF chunk catalog. The tag list and
    /// categorization follow the DS1/DS2 corpus and OpenDS's file-formats
    /// catalog; the tree groups are intentionally broader than the editor
    /// types because many game-data chunks share the same metadata-only
    /// placeholder view until a dedicated editor exists.
    /// </summary>
    public static class ResourceCatalog
    {
        private static readonly Dictionary<string, ResourceTagInfo> Tags = new(StringComparer.Ordinal);
        private static readonly Dictionary<ResourceGroup, ResourceGroupInfo> Groups = new();

        static ResourceCatalog()
        {
            AddGroup(ResourceGroup.Graphics, "Graphics", 10,
                "Sprites, tiles, portraits, icons, walls and still images.");
            AddGroup(ResourceGroup.Palettes, "Palettes", 20,
                "VGA palettes used by images and cinematics.");
            AddGroup(ResourceGroup.Fonts, "Fonts", 30,
                "Bitmap font resources.");
            AddGroup(ResourceGroup.Text, "Text & Names", 40,
                "Game text, spell descriptions, credits, region names and the NAME pool.");
            AddGroup(ResourceGroup.Scripts, "Scripts", 50,
                "Compiled GPL scripts and master scripts.");
            AddGroup(ResourceGroup.Audio, "Audio", 60,
                "Sound samples, music sequences and audio drivers.");
            AddGroup(ResourceGroup.UI, "Interface", 70,
                "Window, button, edit-box and accelerator definitions.");
            AddGroup(ResourceGroup.Cinematics, "Cinematics", 80,
                "BMA frame streams, ACF playback scripts and animation tables.");
            AddGroup(ResourceGroup.World, "World & Maps", 90,
                "Region tile maps, wall maps, entity placements and region data.");
            AddGroup(ResourceGroup.Objects, "Objects & Rules", 100,
                "Object definitions, record tables and item base data.");
            AddGroup(ResourceGroup.GameData, "Game Data", 110,
                "Spell tables and other game rules data.");
            AddGroup(ResourceGroup.Save, "Save & Settings", 120,
                "Character slots, spell lists, preferences and save metadata.");
            AddGroup(ResourceGroup.Internal, "Internal", 130,
                "GFF indexes, free lists and other implementation chunks.");
            AddGroup(ResourceGroup.Unknown, "Unknown", 140,
                "Chunks that are not in the catalog yet.");

            // Graphics.
            Add("BMP ", "Bitmap", ResourceGroup.Graphics, ResourceType.Image,
                "Standard Dark Sun bitmap container (one or more frames).");
            Add("CBMP", "Color bitmap", ResourceGroup.Graphics, ResourceType.Image,
                "DS2 creature color-map animation.");
            Add("ICON", "Icon", ResourceGroup.Graphics, ResourceType.Image,
                "Inventory/spell icon, usually 16x16 with one to four frames.");
            Add("PORT", "Portrait", ResourceGroup.Graphics, ResourceType.Image,
                "Character portrait (32x32 in DS1, 72x72 in DS2).");
            Add("TILE", "Tile", ResourceGroup.Graphics, ResourceType.Image,
                "16x16 region background tile.");
            Add("WALL", "Wall", ResourceGroup.Graphics, ResourceType.Image,
                "Region wall sprite.");
            Add("CMAT", "Cinematic still", ResourceGroup.Graphics, ResourceType.Image,
                "DS1 BMA-codec still image with its own CPAL palette.");
            Add("SPR ", "Sprite", ResourceGroup.Graphics, ResourceType.Image,
                "Sprite bitmap.");
            Add("TGA ", "Targa image", ResourceGroup.Graphics, ResourceType.Image,
                "Targa image (alias tag).");
            Add("BMAP", "Bump map", ResourceGroup.Graphics, ResourceType.RawData,
                "Bump map material chunk.");
            Add("TMAP", "Texture map", ResourceGroup.Graphics, ResourceType.RawData,
                "Texture map material chunk.");
            Add("TXRF", "Texture reference", ResourceGroup.Graphics, ResourceType.RawData,
                "Texture reference material chunk.");
            Add("OMAP", "Opacity map", ResourceGroup.Graphics, ResourceType.RawData,
                "Opacity map material chunk.");
            Add("CMAP", "Color map", ResourceGroup.Graphics, ResourceType.RawData,
                "Color remap table.");

            // Palettes and fonts.
            Add("PAL ", "Palette", ResourceGroup.Palettes, ResourceType.Palette,
                "768-byte VGA palette with 6-bit RGB components.");
            Add("CPAL", "Still palette", ResourceGroup.Palettes, ResourceType.Palette,
                "Per-still palette paired with a CMAT chunk.");
            Add("FONT", "Font", ResourceGroup.Fonts, ResourceType.Font,
                "Bitmap font: glyph offsets, dimensions and pixel bytes.");
            Add("FNT ", "Font (alias)", ResourceGroup.Fonts, ResourceType.Font,
                "Bitmap font under the alias tag.");

            // Text.
            Add("TEXT", "Text", ResourceGroup.Text, ResourceType.Text,
                "Generic text string.");
            Add("MSG ", "Text (alias)", ResourceGroup.Text, ResourceType.Text,
                "Generic text string under the alias tag.");
            Add("SPIN", "Spell description", ResourceGroup.Text, ResourceType.Text,
                "Description shown in the spell book.");
            Add("MERR", "Error text", ResourceGroup.Text, ResourceType.Text,
                "Error and system messages.");
            Add("ETME", "Credits text", ResourceGroup.Text, ResourceType.Text,
                "Copyright and credits text.");
            Add("RNME", "Region name", ResourceGroup.Text, ResourceType.Text,
                "DS2 region display name.");
            Add("NAME", "Name table", ResourceGroup.Text, ResourceType.Name,
                "DS1 packed name/path/index records.");

            // Scripts and script indexes.
            Add("GPL ", "GPL script", ResourceGroup.Scripts, ResourceType.Script,
                "Compiled GPL bytecode.");
            Add("MAS ", "Master script", ResourceGroup.Scripts, ResourceType.Script,
                "Compiled master GPL script.");
            Add("GPLX", "GPL index", ResourceGroup.Internal, ResourceType.RawData,
                "GPL entry index data.");
            Add("GPLI", "GPL entry index", ResourceGroup.Internal, ResourceType.RawData,
                "GPL entry/chunk offset table.");

            // Audio.
            Add("BVOC", "Sound effect", ResourceGroup.Audio, ResourceType.Sound,
                "Creative Voice (VOC) sample.");
            Add("FVOC", "Foreground sound", ResourceGroup.Audio, ResourceType.Sound,
                "Foreground Creative Voice sample.");
            Add("WAV ", "Wave sound", ResourceGroup.Audio, ResourceType.Sound,
                "Wave sound sample.");
            Add("VOC ", "VOC sound", ResourceGroup.Audio, ResourceType.Sound,
                "Creative Voice sample.");
            Add("GSEQ", "General MIDI music", ResourceGroup.Audio, ResourceType.Music,
                "XMI sequence for General MIDI.");
            Add("LSEQ", "Roland music", ResourceGroup.Audio, ResourceType.Music,
                "XMI sequence for Roland/MT-32.");
            Add("PSEQ", "PC speaker music", ResourceGroup.Audio, ResourceType.Music,
                "XMI sequence for the PC speaker.");
            Add("FSEQ", "FM music", ResourceGroup.Audio, ResourceType.Music,
                "XMI sequence for AdLib/OPL FM.");
            Add("CSEQ", "Clock sequence", ResourceGroup.Audio, ResourceType.Music,
                "XMI timer track for cinematic pacing.");
            Add("MSEQ", "Master sequence", ResourceGroup.Audio, ResourceType.Music,
                "Master XMI sequence.");
            Add("ADV ", "AIL/MEL driver", ResourceGroup.Audio, ResourceType.RawData,
                "Audio driver binary.");
            Add("DADV", "Dynamic driver", ResourceGroup.Audio, ResourceType.RawData,
                "Dynamic AIL driver binary.");
            Add("DRV ", "Audio driver", ResourceGroup.Audio, ResourceType.RawData,
                "Generic audio driver binary.");
            Add("MGTL", "Timbre library", ResourceGroup.Audio, ResourceType.RawData,
                "MIDI timbre library.");
            Add("PLYL", "Playlist", ResourceGroup.Audio, ResourceType.RawData,
                "Dead DS2 combat playlist data.");

            // UI.
            Add("WIND", "Window", ResourceGroup.UI, ResourceType.RawData,
                "Window tree with geometry, frame and item list.");
            Add("BUTN", "Button", ResourceGroup.UI, ResourceType.RawData,
                "Button definition (frame, icon, hotkey, text).");
            Add("APFM", "Application frame", ResourceGroup.UI, ResourceType.RawData,
                "Static panel/frame resource referenced by WIND items.");
            Add("EBOX", "Edit box", ResourceGroup.UI, ResourceType.RawData,
                "Edit-box definition.");
            Add("ACCL", "Accelerator table", ResourceGroup.UI, ResourceType.RawData,
                "Keyboard shortcut table.");
            Add("DBOX", "Dialog box", ResourceGroup.UI, ResourceType.RawData,
                "Dialog-box definition.");
            Add("MENU", "Menu", ResourceGroup.UI, ResourceType.RawData,
                "Menu definition.");
            Add("SBAR", "Scroll bar", ResourceGroup.UI, ResourceType.RawData,
                "Scroll-bar definition.");

            // Cinematics.
            Add("BMA ", "BMA animation", ResourceGroup.Cinematics, ResourceType.Animation,
                "DS1 cinematic frame stream.");
            Add("ANM ", "Animation (alias)", ResourceGroup.Cinematics, ResourceType.Animation,
                "Animation chunk under the alias tag.");
            Add("ACF ", "ACF script", ResourceGroup.Cinematics, ResourceType.AnimationControl,
                "Animation control script that drives a BMA stream.");
            Add("SCMD", "Animation command table", ResourceGroup.Cinematics, ResourceType.RawData,
                "Animation command definitions.");
            Add("SJMP", "Animation jump table", ResourceGroup.Cinematics, ResourceType.RawData,
                "Animation jump-table definitions.");

            // World.
            Add("RMAP", "Region tile map (DS1)", ResourceGroup.World, ResourceType.Map,
                "128x98 byte tile grid (DS1).");
            Add("MAP ", "Region tile map (DS2)", ResourceGroup.World, ResourceType.Map,
                "128x98 byte tile grid (DS2).");
            Add("GMAP", "Wall/flag map", ResourceGroup.World, ResourceType.Map,
                "128x98 wall index and passability flags.");
            Add("ETAB", "Entity placements", ResourceGroup.World, ResourceType.RawData,
                "Placed object coordinates and object ids.");
            Add("VECT", "Direction table", ResourceGroup.World, ResourceType.RawData,
                "256-direction unit circle.");
            Add("RDAT", "Region config", ResourceGroup.World, ResourceType.RawData,
                "Per-region binary configuration.");
            Add("MONR", "Region monsters", ResourceGroup.World, ResourceType.RawData,
                "Monster ids and levels by region.");

            // Objects and rules.
            Add("OJFF", "Object definition", ResourceGroup.Objects, ResourceType.RawData,
                "Object template: sprite and flags.");
            Add("RDFF", "Record data", ResourceGroup.Objects, ResourceType.RawData,
                "Per-game record chain (items, creatures, templates).");
            Add("IT1R", "Item base table", ResourceGroup.Objects, ResourceType.Item,
                "DS1 item base-stat table.");
            Add("ITM ", "Item (alias)", ResourceGroup.Objects, ResourceType.Item,
                "Item chunk under the alias tag.");
            Add("CHR ", "Character (alias)", ResourceGroup.Save, ResourceType.Character,
                "Character chunk under the alias tag.");
            Add("FNFO", "Object data table", ResourceGroup.Objects, ResourceType.RawData,
                "Object data index table.");
            Add("POBJ", "Polymesh objects", ResourceGroup.Objects, ResourceType.RawData,
                "Polymesh object database.");
            Add("ALL ", "Object digests", ResourceGroup.Objects, ResourceType.RawData,
                "Aggregate object-database tables.");

            // Game data.
            Add("DATA", "Spell data", ResourceGroup.GameData, ResourceType.RawData,
                "DS2 spell records and auxiliary rules tables.");

            // Save and settings.
            Add("PREF", "Preferences", ResourceGroup.Save, ResourceType.RawData,
                "Text speed, music/sound volume and input toggles.");
            Add("CHAR", "Character slot", ResourceGroup.Save, ResourceType.Character,
                "Saved character record.");
            Add("SPST", "Spell list", ResourceGroup.Save, ResourceType.Character,
                "Known-spell bitmask.");
            Add("PSST", "Psionic list", ResourceGroup.Save, ResourceType.Character,
                "Known-psionic bytes.");
            Add("PSIN", "Psionic selection", ResourceGroup.Save, ResourceType.Character,
                "Psionic and sphere selections.");
            Add("CACT", "Character flag", ResourceGroup.Save, ResourceType.Character,
                "Valid-character flag.");
            Add("STXT", "Save text", ResourceGroup.Save, ResourceType.RawData,
                "Save-game text.");
            Add("SAVE", "Save metadata", ResourceGroup.Save, ResourceType.RawData,
                "Save-game state block.");
            Add("GREQ", "Save view state", ResourceGroup.Save, ResourceType.RawData,
                "Saved camera bounds and picture index.");

            // Structural/internal.
            Add("GFFI", "GFF index", ResourceGroup.Internal, ResourceType.RawData,
                "GFF table-of-contents/index chunk.");
            Add("FORM", "Form", ResourceGroup.Internal, ResourceType.RawData,
                "IFF-like form grouping.");
            Add("GFRE", "Free list", ResourceGroup.Internal, ResourceType.RawData,
                "GFF free-space list.");
            Add("GTOC", "Table of contents", ResourceGroup.Internal, ResourceType.RawData,
                "GFF table of contents.");
        }

        public static ResourceTagInfo Classify(string tag)
        {
            string key = NormalizeTag(tag);
            if (Tags.TryGetValue(key, out ResourceTagInfo info))
                return info;

            string display = string.IsNullOrWhiteSpace(tag) ? "Unknown" : tag.Trim();
            return new ResourceTagInfo(key, display, ResourceGroup.Unknown, ResourceType.Unknown,
                "Chunk is not in the resource catalog yet.");
        }

        public static ResourceGroupInfo GetGroup(ResourceGroup group)
        {
            return Groups.TryGetValue(group, out ResourceGroupInfo info)
                ? info
                : new ResourceGroupInfo(group, group.ToString(), 999, string.Empty);
        }

        /// <summary>
        /// Human-readable labels for well-known resources. The main menu is
        /// especially non-obvious: its buttons contain no text strings; the
        /// flaming labels are the bitmap glyphs in ICON 2048..2051.
        /// </summary>
        public static string GetKnownDisplayName(string tag, int number)
        {
            return NormalizeTag(tag) switch
            {
                "WIND" when number == 3000 => "Main menu window",
                "WIND" when number == 10500 => "Game menu window (ESC)",
                "WIND" when number == 16500 => "Preferences window",
                "WIND" when number == 3009 => "Load/Save window",
                "WIND" when number == 3011 => "Character creation window",
                "WIND" when number == 13500 => "Inventory window",
                "WIND" when number == 11500 => "Character sheet window",
                "WIND" when number == 15503 => "Spell info window",
                "BUTN" when number >= 2048 && number <= 2051 =>
                    $"Main menu button: {MainMenuLabel(number)}",
                "ICON" when number >= 2048 && number <= 2051 =>
                    $"Menu label: {MainMenuLabel(number)}",
                "ACCL" when number == 8100 => "Main menu hotkeys (S/C/L/E)",
                "BMP " when number == 20028 => "Main menu burning arc",
                "BMP " when number == 20029 => "Main menu stone panel",
                _ => null
            };
        }

        public static string GetKnownDescription(string tag, int number)
        {
            return NormalizeTag(tag) switch
            {
                "WIND" when number == 3000 =>
                    "Initial menu. Items: ACCL 8100 + BUTN 2048, 2049, 2050, 2051. " +
                    "The button captions are bitmap labels in ICON 2048..2051, not text strings.",
                "BUTN" when number >= 2048 && number <= 2051 =>
                    $"Initial-menu button '{MainMenuLabel(number)}'. Its icon_id points at ICON {number}; " +
                    "the visible caption is baked into that bitmap.",
                "ICON" when number >= 2048 && number <= 2051 =>
                    $"Initial-menu caption '{MainMenuLabel(number)}' as a 4-frame bitmap; " +
                    "edit these pixels to translate the main menu.",
                "ACCL" when number == 8100 =>
                    "Main-menu accelerator table: S/s Start, C/c Create, L/l Load, E/e Exit.",
                "BMP " when number == 20028 =>
                    "Burning arc furniture behind the initial menu.",
                "BMP " when number == 20029 =>
                    "Ornate stone panel behind the initial menu.",
                _ => null
            };
        }

        private static string MainMenuLabel(int buttonId)
        {
            return buttonId switch
            {
                2048 => "START GAME",
                2049 => "CREATE CHARACTER",
                2050 => "LOAD SAVED GAME",
                2051 => "EXIT TO DOS",
                _ => "menu button"
            };
        }

        public static IReadOnlyList<ResourceGroupInfo> AllGroups =>
            Groups.Values.OrderBy(group => group.Order).ToList();

        public static string NormalizeTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return "    ";
            return tag.Length >= 4 ? tag.Substring(0, 4) : tag.PadRight(4);
        }

        private static void Add(
            string tag,
            string displayName,
            ResourceGroup group,
            ResourceType type,
            string description)
        {
            Tags[tag] = new ResourceTagInfo(tag, displayName, group, type, description);
        }

        private static void AddGroup(ResourceGroup group, string displayName, int order, string description)
        {
            Groups[group] = new ResourceGroupInfo(group, displayName, order, description);
        }
    }
}
