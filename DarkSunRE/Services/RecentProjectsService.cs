using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DarkSunRE.Services
{
    public sealed class RecentProjectEntry
    {
        public RecentProjectEntry(string path)
        {
            FullPath = path;
            DisplayName = Path.GetFileNameWithoutExtension(path);
        }

        public string FullPath { get; }
        public string DisplayName { get; }
    }

    /// <summary>
    /// Persists the last few successfully opened project files outside the
    /// game directories, under %APPDATA%\DarkSunRE.
    /// </summary>
    public sealed class RecentProjectsService
    {
        private const int MaximumCount = 3;
        private readonly string _settingsPath;

        public RecentProjectsService()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DarkSunRE",
                "recent-projects.json");
        }

        public IReadOnlyList<RecentProjectEntry> Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return Array.Empty<RecentProjectEntry>();

                var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_settingsPath)) ?? new List<string>();
                return paths
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumCount)
                    .Select(path => new RecentProjectEntry(path))
                    .ToList();
            }
            catch
            {
                return Array.Empty<RecentProjectEntry>();
            }
        }

        public IReadOnlyList<RecentProjectEntry> Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Load();

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return Load();
            }

            var paths = Load()
                .Select(entry => entry.FullPath)
                .Where(existing => !string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            paths.Insert(0, fullPath);
            paths = paths.Take(MaximumCount).ToList();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(paths));
            }
            catch
            {
                // Recent-project persistence must never break opening a project.
            }

            return paths.Select(pathItem => new RecentProjectEntry(pathItem)).ToList();
        }
    }
}
