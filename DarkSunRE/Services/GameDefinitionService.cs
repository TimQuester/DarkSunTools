using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DarkSunRE.Services
{
    public class GameDefinitionService : IGameDefinitionService
    {        
        private readonly Dictionary<ProjectType, GameDefinition> _gameDefinitions;
        private readonly Dictionary<string, ImageSource> _iconCache;

        public GameDefinitionService()
        {            
            _gameDefinitions = new Dictionary<ProjectType, GameDefinition>();
            _iconCache = new Dictionary<string, ImageSource>();

            InitializeGameDefinitions();
        }

        public ObservableCollection<GameDefinition> GetSupportedGames()
        {
            return new ObservableCollection<GameDefinition>(_gameDefinitions.Values.OrderBy(g => g.Name).ToList());
        }

        public GameDefinition GetGameDefinition(ProjectType type)
        {
            return _gameDefinitions.TryGetValue(type, out var definition)
                ? definition
                : null;
        }

        public bool ValidateGamePath(string gamePath, ProjectType gameType)
        {            
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return false;

            var definition = GetGameDefinition(gameType);
            if (definition == null)
                return false;

            // Базовая проверка на существование файлов
            foreach (var requiredFile in definition.RequiredFiles)
            {
                var filePath = Path.Combine(gamePath, requiredFile);
                if (!File.Exists(filePath))
                    return false;
            }

            return true;
        }

        public string[] GetExpectedFiles(ProjectType gameType)
        {
            var definition = GetGameDefinition(gameType);
            return definition?.RequiredFiles ?? Array.Empty<string>();
        }

        public string[] GetGffFilePatterns(ProjectType gameType)
        {
            return gameType switch
            {
                ProjectType.DarkSunShatteredLands => new[]
                {
                    "*.gff",
                },
                ProjectType.DarkSunWakeOfTheRavager => new[]
                {
                    "*.gff",
                },
                ProjectType.AlQuadin => new[]
                {
                    "*.*",
                },
                _ => new[] { "*.*" }
            };
        }

        public string DetectGameVersion(string gamePath, ProjectType gameType)
        {
            var definition = GetGameDefinition(gameType);
            if (definition == null || string.IsNullOrEmpty(definition.DefaultExecutable))
                return "Unknown";

            var exePath = Path.Combine(gamePath, definition.DefaultExecutable);
            if (!File.Exists(exePath))
                return "Not Found";

            try
            {
                var fileInfo = new FileInfo(exePath);
                var fileSize = fileInfo.Length;
                var fileVersion = FileVersionInfo.GetVersionInfo(exePath);

                foreach (var version in definition.KnownVersions)
                {
                    if (fileSize == version.ExecutableSize)
                        return version.Version;
                }

                return fileVersion.FileVersion ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public ImageSource GetGameIcon(ProjectType gameType)
        {
            var definition = GetGameDefinition(gameType);
            if (definition == null || string.IsNullOrEmpty(definition.IconResourcePath))
                return GetDefaultIcon();

            if (_iconCache.TryGetValue(definition.IconResourcePath, out var cachedIcon))
                return cachedIcon;

            try
            {
                var icon = LoadIconFromResource(definition.IconResourcePath);
                _iconCache[definition.IconResourcePath] = icon;
                return icon;
            }
            catch
            {
                return GetDefaultIcon();
            }
        }

        public string GetGameExecutable(ProjectType gameType)
        {
            var definition = GetGameDefinition(gameType);
            return definition?.DefaultExecutable ?? string.Empty;
        }

        private void InitializeGameDefinitions()
        {
            // Dark Sun: Shattered Lands
            _gameDefinitions[ProjectType.DarkSunShatteredLands] = new GameDefinition
            {
                Type = ProjectType.DarkSunShatteredLands,
                Name = "Dark Sun: Shattered Lands",
                Description = "SSI's classic RPG set in the Dark Sun universe",
                Developer = "Strategic Simulations, Inc.",
                Publisher = "Strategic Simulations, Inc.",
                ReleaseYear = 1993,
                DefaultExecutable = "DSUN.EXE",
                RequiredFiles = new[] { "DSUN.EXE", "RGN0A.GFF", "GPLDATA.GFF" },
                OptionalFiles = new[] { "SOUND.GFF", "MOVIE.GFF", "SAVE*.*" },
                SupportedFileFormats = new[] { "*.GFF"},
                IconResourcePath = "pack://application:,,,/Resources/Images/darksun1.png",
                KnownVersions = new[]
                {
                    new VersionInfo { Version = "1.0", ExecutableSize = 249856, Description = "Original release" },
                    new VersionInfo { Version = "1.1", ExecutableSize = 250368, Description = "Patched version" }
                },
                FileDescriptions = new Dictionary<string, string>
                {
                    ["DATA.GFF"] = "Main game data file",
                    ["TEXT.GFF"] = "Text and dialogue resources",
                    ["SOUND.GFF"] = "Sound effects and music",
                    ["MOVIE.GFF"] = "Cinematic sequences"
                }
            };

            // Dark Sun: Wake of the Ravager
            _gameDefinitions[ProjectType.DarkSunWakeOfTheRavager] = new GameDefinition
            {
                Type = ProjectType.DarkSunWakeOfTheRavager,
                Name = "Dark Sun: Wake of the Ravager",
                Description = "Sequel to Shattered Lands",
                Developer = "Strategic Simulations, Inc.",
                Publisher = "Strategic Simulations, Inc.",
                ReleaseYear = 1994,
                DefaultExecutable = "DSUN2.EXE",
                RequiredFiles = new[] { "DSUN2.EXE", "DATA.GFF", "TEXT.GFF" },
                OptionalFiles = new[] { "SOUND.GFF", "MOVIE.GFF" },
                SupportedFileFormats = new[] { "*.GFF"},
                IconResourcePath = "pack://application:,,,/Resources/Images/darksun2.png",
                KnownVersions = new[]
                {
                    new VersionInfo { Version = "1.0", ExecutableSize = 278528, Description = "Original release" }
                }
            };

            // Al-Qadim: The Genie's Curse
            _gameDefinitions[ProjectType.AlQuadin] = new GameDefinition
            {
                Type = ProjectType.AlQuadin,
                Name = "Al-Qadim: The Genie's Curse",
                Description = "Game in Al-Quadin setting",
                Developer = "Cyberlore Studios",
                Publisher = "Strategic Simulations, Inc.",
                ReleaseYear = 1994,
                DefaultExecutable = "GENIER.EXE",
                RequiredFiles = new[] { "GENIER.EXE", "GPL2.COD", "GPL8.COD" },
                OptionalFiles = new[] { "1.VOC", "ST1_GR1.XMI" },
                SupportedFileFormats = new[] { "*.GFF", "*.RES" },
                IconResourcePath = "pack://application:,,,/Resources/Images/alquadin.png",
                KnownVersions = new[]
                {
                    new VersionInfo { Version = "1.0", ExecutableSize = 278528, Description = "Original release" }
                }
            };
        }

        private ImageSource LoadIconFromResource(string resourcePath)
        {
            var uri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);
            return new BitmapImage(uri);
        }

        private ImageSource GetDefaultIcon()
        {
            return LoadIconFromResource("pack://application:,,,/Resources/Images/default_game.png");
        }
    }
}
