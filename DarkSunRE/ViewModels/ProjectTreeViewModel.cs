using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using DSunTools.Common.Gpl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace DarkSunRE.ViewModels
{
    public class ProjectTreeViewModel : INotifyPropertyChanged
    {
        private readonly IProjectService _projectService;
        private readonly IStatusService _statusService;
        private readonly IResourceService _resourceService;
        private Project _currentProject;

        // Фильтры по типам ресурсов
        private bool _showImages = true;
        private bool _showPalettes = true;
        private bool _showFonts = true;
        private bool _showTexts = true;
        private bool _showScripts = true;
        private bool _showSounds = true;
        private bool _showMusic = true;
        private bool _showDialogs = true;
        private bool _showItems = true;
        private bool _showCharacters = true;
        private bool _showMaps = true;
        private bool _showAnimations = true;
        private bool _showRawData = true;
        private bool _showAnimationControls = true;
        private bool _showNames = true;
        private bool _showUnknown = true;

        public ICommand NodeDoubleClickedCommand { get; }
        public ICommand GenerateNewFileCommand { get; }
        public ICommand SaveAllJsonsCommand { get; }
        public ICommand LoadAllJsonsCommand { get; }
        public ICommand SaveAllTextJsonCommand { get; }
        public ICommand LoadAllTextJsonCommand { get; }
        public ICommand ToggleFilterCommand { get; }
        public ICommand SelectAllFiltersCommand { get; }
        public ICommand DeselectAllFiltersCommand { get; }

        public ProjectTreeViewModel(IProjectService projectService, IStatusService statusService, IResourceService resourceService)
        {
            _projectService = projectService;
            _statusService = statusService;
            _resourceService = resourceService;

            RootNodes = new ObservableCollection<ProjectNode>();
            FilteredRootNodes = new ObservableCollection<ProjectNode>();

            // Команды для работы с деревом
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            RefreshCommand = new RelayCommand(RefreshTree);
            NodeDoubleClickedCommand = new RelayCommand<ProjectNode>(OnNodeDoubleClicked);
            GenerateNewFileCommand = new RelayCommand<ProjectNode>(GenerateNewFile);
            SaveAllJsonsCommand = new RelayCommand<ProjectNode>(SaveAllJsons);
            LoadAllJsonsCommand = new RelayCommand<ProjectNode>(LoadAllJsons);
            SaveAllTextJsonCommand = new RelayCommand<ProjectNode>(SaveAllTextJson);
            LoadAllTextJsonCommand = new RelayCommand<ProjectNode>(LoadAllTextJson);
            ToggleFilterCommand = new RelayCommand<string>(ToggleFilter);
            SelectAllFiltersCommand = new RelayCommand(SelectAllFilters);
            DeselectAllFiltersCommand = new RelayCommand(DeselectAllFilters);
        }

        public Project CurrentProject
        {
            get => _currentProject;
            set
            {
                if (SetProperty(ref _currentProject, value))
                {
                    LoadProjectTree();
                }
            }
        }

        public ObservableCollection<ProjectNode> RootNodes { get; }
        public ObservableCollection<ProjectNode> FilteredRootNodes { get; }

        // Свойства фильтров
        public bool ShowImages
        {
            get => _showImages;
            set
            {
                SetProperty(ref _showImages, value);
                ApplyFilters();
            }
        }

        public bool ShowPalettes
        {
            get => _showPalettes;
            set
            {
                SetProperty(ref _showPalettes, value);
                ApplyFilters();
            }
        }

        public bool ShowFonts
        {
            get => _showFonts;
            set
            {
                SetProperty(ref _showFonts, value);
                ApplyFilters();
            }
        }

        public bool ShowTexts
        {
            get => _showTexts;
            set
            {
                SetProperty(ref _showTexts, value);
                ApplyFilters();
            }
        }

        public bool ShowScripts
        {
            get => _showScripts;
            set
            {
                SetProperty(ref _showScripts, value);
                ApplyFilters();
            }
        }

        public bool ShowSounds
        {
            get => _showSounds;
            set
            {
                SetProperty(ref _showSounds, value);
                ApplyFilters();
            }
        }

        public bool ShowMusic
        {
            get => _showMusic;
            set
            {
                SetProperty(ref _showMusic, value);
                ApplyFilters();
            }
        }

        public bool ShowDialogs
        {
            get => _showDialogs;
            set
            {
                SetProperty(ref _showDialogs, value);
                ApplyFilters();
            }
        }

        public bool ShowItems
        {
            get => _showItems;
            set
            {
                SetProperty(ref _showItems, value);
                ApplyFilters();
            }
        }

        public bool ShowCharacters
        {
            get => _showCharacters;
            set
            {
                SetProperty(ref _showCharacters, value);
                ApplyFilters();
            }
        }

        public bool ShowMaps
        {
            get => _showMaps;
            set
            {
                SetProperty(ref _showMaps, value);
                ApplyFilters();
            }
        }

        public bool ShowAnimations
        {
            get => _showAnimations;
            set
            {
                SetProperty(ref _showAnimations, value);
                ApplyFilters();
            }
        }

        public bool ShowRawData
        {
            get => _showRawData;
            set
            {
                SetProperty(ref _showRawData, value);
                ApplyFilters();
            }
        }

        public bool ShowAnimationControls
        {
            get => _showAnimationControls;
            set
            {
                SetProperty(ref _showAnimationControls, value);
                ApplyFilters();
            }
        }

        public bool ShowUnknown
        {
            get => _showUnknown;
            set
            {
                SetProperty(ref _showUnknown, value);
                ApplyFilters();
            }
        }
        
        public bool ShowNames
        {
            get => _showNames;
            set
            {
                SetProperty(ref _showNames, value);
                ApplyFilters();
            }
        }

        private void SelectAllFilters()
        {
            ShowImages = true;
            ShowPalettes = true;
            ShowFonts = true;
            ShowTexts = true;
            ShowScripts = true;
            ShowSounds = true;
            ShowMusic = true;
            ShowDialogs = true;
            ShowItems = true;
            ShowCharacters = true;
            ShowMaps = true;
            ShowAnimations = true;
            ShowRawData = true;
            ShowAnimationControls = true;
            ShowNames = true;
            ShowUnknown = true;
        }

        private void DeselectAllFilters()
        {
            ShowImages = false;
            ShowPalettes = false;
            ShowFonts = false;
            ShowTexts = false;
            ShowScripts = false;
            ShowSounds = false;
            ShowMusic = false;
            ShowDialogs = false;
            ShowItems = false;
            ShowCharacters = false;
            ShowMaps = false;
            ShowAnimations = false;
            ShowRawData = false;
            ShowAnimationControls = false;
            ShowNames = false;
            ShowUnknown = false;
        }

        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand DoubleClickCommand { get; }
        public Func<bool> CanGenerateNewFile { get; private set; }

        private void SaveAllJsons(ProjectNode node)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.JsonFolderPath))
            {
                _statusService.ShowError("JSON folder path is not configured.");
                return;
            }

            if (node?.Item is not ResourceFile file || file.Resources == null || file.Data == null)
            {
                _statusService.ShowError("Invalid or unloaded GFF file selection.");
                return;
            }

            try
            {
                Directory.CreateDirectory(CurrentProject.JsonFolderPath);
                var gff = _resourceService.ParseGffFile(file.Data);
                var scripts = file.Resources.Where(resource => resource.Type == ResourceType.Script).ToList();
                int savedCount = 0;

                foreach (ResourceInfo resource in scripts)
                {
                    var document = FindOpenedGplDocument(file, resource) ?? CreateGplDocument(file, resource, gff);
                    new GplDocumentViewModel(document).SaveJson(CurrentProject.JsonFolderPath);
                    savedCount++;
                }

                _statusService.ShowSuccess($"Saved {savedCount} GPL JSON file(s) from {file.Name}.");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to export GPL JSON files from {file.Name}: {ex.Message}", ex);
            }
        }

        private void LoadAllJsons(ProjectNode node)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.JsonFolderPath))
            {
                _statusService.ShowError("JSON folder path is not configured.");
                return;
            }

            if (node?.Item is not ResourceFile file || file.Resources == null || file.Data == null)
            {
                _statusService.ShowError("Invalid or unloaded GFF file selection.");
                return;
            }

            try
            {
                var gff = _resourceService.ParseGffFile(file.Data);
                var scripts = file.Resources.Where(resource => resource.Type == ResourceType.Script).ToList();
                int loadedCount = 0;
                int missingCount = 0;

                foreach (ResourceInfo resource in scripts)
                {
                    string jsonPath = Path.Combine(CurrentProject.JsonFolderPath, $"{file.Name}_{resource.Tag}-{resource.Number}.json");
                    if (!File.Exists(jsonPath))
                    {
                        missingCount++;
                        continue;
                    }

                    GPLDocument document = FindOpenedGplDocument(file, resource) ?? CreateGplDocument(file, resource, gff);
                    if (!CurrentProject.OpenedDocuments.Contains(document))
                        CurrentProject.OpenedDocuments.Add(document);

                    new GplDocumentViewModel(document).LoadJson(jsonPath);
                    loadedCount++;
                }

                _statusService.ShowSuccess($"Loaded {loadedCount} GPL JSON file(s) from {file.Name}; {missingCount} missing.");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to import GPL JSON files from {file.Name}: {ex.Message}", ex);
            }
        }

        private GPLDocument FindOpenedGplDocument(ResourceFile file, ResourceInfo resource)
        {
            return CurrentProject.OpenedDocuments.FirstOrDefault(document =>
                string.Equals(document.FileName, file.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(document.ResourceName, $"{resource.Tag}-{resource.Number}", StringComparison.OrdinalIgnoreCase));
        }

        private GPLDocument CreateGplDocument(ResourceFile file, ResourceInfo resource, DSunTools.Common.Gff.GffFile gff)
        {
            byte[] data = _resourceService.GetResourceData(gff, resource.Tag, resource.Number);
            string resourceName = $"{resource.Tag}-{resource.Number}";
            return new GPLDocument(data, $"{file.Name}:{resourceName}", file.FullPath, file.Name, resourceName,
                CurrentProject.TransliterationRules);
        }

        /// <summary>
        /// Exports all text resources (TEXT, MSG, MERR, ETME, SPIN, RNME, NAME) to a single JSON file.
        /// </summary>
        private void SaveAllTextJson(ProjectNode node)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.JsonFolderPath))
            {
                _statusService.ShowError("JSON folder path is not configured.");
                return;
            }

            try
            {
                Directory.CreateDirectory(CurrentProject.JsonFolderPath);
                var export = new TextResourcesExport
                {
                    ProjectName = CurrentProject.ProjectName,
                    ExportDate = DateTime.Now
                };
                var errors = new List<string>();

                foreach (ResourceFile resourceFile in CurrentProject.Files)
                {
                    if (resourceFile.Data == null)
                    {
                        errors.Add($"{resourceFile.Name}: file data is not loaded.");
                        continue;
                    }

                    DSunTools.Common.Gff.GffFile gff;
                    try
                    {
                        gff = _resourceService.ParseGffFile(resourceFile.Data);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{resourceFile.Name}: {ex.Message}");
                        continue;
                    }

                    foreach (ResourceInfo resource in resourceFile.Resources?.Where(item => item.Type == ResourceType.Text || item.Type == ResourceType.Name)
                                 ?? Enumerable.Empty<ResourceInfo>())
                    {
                        try
                        {
                            byte[] originalData = _resourceService.GetResourceData(gff, resource.Tag, resource.Number)
                                ?? throw new InvalidDataException("Resource data is unavailable.");
                            byte[] currentData = resource.ModifiedData ?? originalData;
                            var entry = new TextResourceEntry
                            {
                                ContainerName = resourceFile.Name,
                                ResourceId = $"{resource.Tag}-{resource.Number}",
                                ResourceType = resource.Type.ToString()
                            };

                            if (resource.Type == ResourceType.Text)
                            {
                                entry.OriginalText = DecodeGameText(originalData);
                                entry.TranslatedText = DecodeGameText(currentData);
                            }
                            else
                            {
                                NameResource originalNames = ReadNameResource(originalData);
                                NameResource currentNames = ReadNameResource(currentData);
                                entry.NameEntries = ExportNameEntries(originalNames, currentNames);
                            }

                            export.Resources.Add(entry);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{resourceFile.Name}:{resource.Tag}-{resource.Number}: {ex.Message}");
                        }
                    }
                }

                string fileName = $"{CurrentProject.ProjectName}_texts.json";
                string fullPath = Path.Combine(CurrentProject.JsonFolderPath, fileName);
                using var fs = new FileStream(fullPath, FileMode.Create);
                JsonSerializer.Serialize(fs, export, new JsonSerializerOptions { WriteIndented = true });

                string message = $"Exported {export.Resources.Count} text resource(s) to {fileName}.";
                if (errors.Count == 0)
                    _statusService.ShowSuccess(message);
                else
                    _statusService.ShowWarning($"{message} Some resources failed:\n{FormatResourceErrors(errors)}");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to export text resources: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Imports all text resources from a single JSON file and updates ResourceInfo.ModifiedData.
        /// </summary>
        private void LoadAllTextJson(ProjectNode node)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.JsonFolderPath))
            {
                _statusService.ShowError("JSON folder path is not configured.");
                return;
            }

            try
            {
                string fileName = $"{CurrentProject.ProjectName}_texts.json";
                string fullPath = Path.Combine(CurrentProject.JsonFolderPath, fileName);
                if (!File.Exists(fullPath))
                {
                    _statusService.ShowError($"Text resource file {fileName} not found.");
                    return;
                }

                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                TextResourcesExport import = JsonSerializer.Deserialize<TextResourcesExport>(fs);
                if (import?.Resources == null || import.Resources.Count == 0)
                {
                    _statusService.ShowWarning("No text resources found in the JSON file.");
                    return;
                }

                int loadedCount = 0;
                var errors = new List<string>();
                foreach (TextResourceEntry entry in import.Resources)
                {
                    string label = $"{entry?.ContainerName ?? "(legacy JSON)"}:{entry?.ResourceId ?? "(missing ID)"}";
                    try
                    {
                        if (entry == null)
                            throw new InvalidDataException("Entry is null.");

                        (ResourceFile file, ResourceInfo resource) = FindTextResource(entry.ContainerName, entry.ResourceId);
                        if (!string.Equals(entry.ResourceType, resource.Type.ToString(), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException($"JSON type '{entry.ResourceType}' does not match resource type '{resource.Type}'.");

                        resource.ModifiedData = EncodeResourceText(resource, entry);
                        resource.IsModified = true;
                        file.IsModified = true;
                        loadedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{label}: {ex.Message}");
                    }
                }

                string message = $"Loaded {loadedCount} text resource(s) from {fileName}.";
                if (errors.Count == 0)
                    _statusService.ShowSuccess(message);
                else
                    _statusService.ShowWarning($"{message} Some entries failed:\n{FormatResourceErrors(errors)}");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to import text resources: {ex.Message}", ex);
            }
        }

        private (ResourceFile File, ResourceInfo Resource) FindTextResource(string containerName, string resourceId)
        {
            int separator = resourceId?.LastIndexOf('-') ?? -1;
            if (separator <= 0 || !int.TryParse(resourceId.Substring(separator + 1), out int number))
                throw new InvalidDataException($"Invalid resource ID '{resourceId}'. Expected TAG-NUMBER.");

            string tag = resourceId.Substring(0, separator);
            var matches = CurrentProject.Files
                .Where(file => string.IsNullOrWhiteSpace(containerName) || string.Equals(file.Name, containerName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(file => (file.Resources ?? new List<ResourceInfo>())
                    .Where(resource => string.Equals(resource.Tag, tag, StringComparison.OrdinalIgnoreCase) && resource.Number == number)
                    .Select(resource => (File: file, Resource: resource)))
                .ToList();

            if (matches.Count == 0)
                throw new KeyNotFoundException($"Resource '{resourceId}' was not found in container '{containerName}'.");
            if (matches.Count > 1)
                throw new InvalidDataException($"Resource '{resourceId}' is ambiguous without a unique container name.");
            return matches[0];
        }

        private List<NameResourceEntryExport> ExportNameEntries(NameResource original, NameResource current)
        {
            var result = new List<NameResourceEntryExport>(current.Entries.Count);
            for (int i = 0; i < current.Entries.Count; i++)
            {
                NameEntry currentEntry = current.Entries[i];
                string originalName = i < original.Entries.Count ? DecodeGameText(original.Entries[i].MainName) : string.Empty;
                result.Add(new NameResourceEntryExport
                {
                    OriginalMainName = originalName,
                    TranslatedMainName = DecodeGameText(currentEntry.MainName),
                    Path = currentEntry.Path,
                    Index = currentEntry.Index,
                    Flag = currentEntry.Flag
                });
            }
            return result;
        }

        private NameResource ReadNameResource(byte[] data)
        {
            var nameResource = new NameResource();
            nameResource.LoadFromBytes(data);
            return nameResource;
        }

        private string DecodeGameText(string text)
        {
            return GplTransliteration.DecodeToDisplay(text ?? string.Empty, GetResourceTransliterationRules());
        }

        private string DecodeGameText(byte[] data)
        {
            return DecodeGameText(Encoding.Latin1.GetString(data).TrimEnd('\0'));
        }

        private string EncodeGameText(string text)
        {
            string gameText = GplTransliteration.EncodeToGameCharset(text ?? string.Empty, GetResourceTransliterationRules());
            if (gameText.Any(character => character > byte.MaxValue))
                throw new InvalidOperationException("Text contains characters without a one-byte game glyph mapping. Add a transliteration rule.");
            return gameText;
        }

        private List<GplTransliterationRule> GetResourceTransliterationRules()
        {
            return CurrentProject.TransliterationRules?
                .Where(rule => rule != null)
                .Select(rule => new GplTransliterationRule(rule.From ?? string.Empty, rule.To ?? string.Empty))
                .ToList() ?? new List<GplTransliterationRule>();
        }

        private byte[] EncodeResourceText(ResourceInfo resource, TextResourceEntry entry)
        {
            if (resource.Type == ResourceType.Text)
            {
                if (entry.TranslatedText == null)
                    throw new InvalidDataException("TEXT entry has no TranslatedText value.");
                return Encoding.Latin1.GetBytes(EncodeGameText(entry.TranslatedText));
            }

            if (resource.Type == ResourceType.Name)
            {
                if (entry.NameEntries == null)
                    throw new InvalidDataException("NAME entry has no NameEntries data; export the current JSON format before importing.");

                var names = new NameResource();
                foreach (NameResourceEntryExport exportedName in entry.NameEntries)
                {
                    if (exportedName == null)
                        throw new InvalidDataException("NAME record is null.");
                    names.Entries.Add(new NameEntry
                    {
                        MainName = exportedName.TranslatedMainName ?? exportedName.OriginalMainName ?? string.Empty,
                        Path = exportedName.Path ?? string.Empty,
                        Index = exportedName.Index,
                        Flag = exportedName.Flag
                    });
                }

                return names.SaveToBytes(GetResourceTransliterationRules());
            }

            throw new InvalidOperationException($"Unsupported resource type: {resource.Type}");
        }

        private static string FormatResourceErrors(List<string> errors)
        {
            const int maximumShown = 8;
            string details = string.Join(Environment.NewLine, errors.Take(maximumShown));
            if (errors.Count > maximumShown)
                details += Environment.NewLine + $"... and {errors.Count - maximumShown} more.";
            return details;
        }

        private void GenerateNewFile(ProjectNode node)
        {
            if (node == null || node.ItemType != ProjectItemType.GffFile || CurrentProject == null)
                return;

            if (string.IsNullOrWhiteSpace(CurrentProject.ModifiedGamePath))
            {
                _statusService.ShowError("Modified Game Path is not configured.");
                return;
            }

            if (node.Item is not ResourceFile file || file.Data == null)
            {
                _statusService.ShowError("Selected file is not loaded.");
                return;
            }

            try
            {
                var gff = _resourceService.ParseGffFile(file.Data);
                var updatedBytes = file.Data;

                if (file.Resources != null)
                {
                    foreach (var resource in file.Resources)
                    {
                        var doc = CurrentProject.OpenedDocuments.FirstOrDefault(d =>
                            string.Equals(d.FileName, file.Name, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(d.ResourceName, $"{resource.Tag}-{resource.Number}", StringComparison.OrdinalIgnoreCase));

                        byte[] replacement = resource.ModifiedData;
                        if (doc != null)
                        {
                            doc.TransliterationRules = CurrentProject.TransliterationRules ?? new List<TransliterationRule>();
                            doc.ApplyStringChanges();
                            replacement = doc.newBytes;
                        }

                        if (replacement != null)
                        {
                            updatedBytes = _resourceService.ReplaceResource(gff, resource.Tag, resource.Number, replacement);
                            gff = _resourceService.ParseGffFile(updatedBytes); // refresh tables for next replace
                        }
                    }
                }

                var outputPath = Path.Combine(CurrentProject.ModifiedGamePath, file.Name);
                File.WriteAllBytes(outputPath, updatedBytes);

                // refresh in-memory data for subsequent openings
                file.Data = updatedBytes;

                _statusService.ShowSuccess($"Generated file: {outputPath}");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to generate file: {ex.Message}", ex);
            }
        }

        private void LoadProjectTree()
        {
            RootNodes.Clear();
            FilteredRootNodes.Clear();

            if (CurrentProject == null)
                return;

            try
            {
                //_statusService.ShowProgress("Loading project tree...");

                var projectNode = new ProjectNode
                {
                    Name = CurrentProject.ProjectName,
                    ItemType = ProjectItemType.Project,
                    Item = CurrentProject,
                    IsExpanded = true
                };

                // Загружаем GFF файлы
                foreach (var file in CurrentProject.Files)
                {
                    var fileNode = new ProjectNode
                    {
                        Name = $"{file.Name} ({file.Resources?.Count ?? 0} resources)",
                        ItemType = ProjectItemType.GffFile,
                        Item = file,
                        IsExpanded = false
                    };

                    // Загружаем file
                    if (file.Resources != null)
                    {
                        foreach (var resource in file.Resources)
                        {
                            var resourceNode = new ProjectNode
                            {
                                Name = $"{resource.Tag}-{resource.Number} ({resource.Type})",
                                ItemType = ProjectItemType.Resource,
                                Item = resource,
                                Tag = resource.Tag,
                                ResourceNumber = resource.Number
                            };

                            fileNode.Children.Add(resourceNode);
                        }
                    }

                    projectNode.Children.Add(fileNode);
                }

                RootNodes.Add(projectNode);
                
                // Применяем фильтры
                ApplyFilters();
                
                _statusService.ShowSuccess("Project tree loaded");
            }
            catch (Exception ex)
            {
                _statusService.ShowError("Failed to load project tree", ex);
            }
        }

        private void ApplyFilters()
        {
            FilteredRootNodes.Clear();

            foreach (var rootNode in RootNodes)
            {
                var filteredNode = FilterNode(rootNode);
                if (filteredNode != null)
                {
                    FilteredRootNodes.Add(filteredNode);
                }
            }
        }

        private ProjectNode FilterNode(ProjectNode node)
        {
            // Для проекта и GFF файлов создаем копии с детьми
            if (node.ItemType == ProjectItemType.Project || node.ItemType == ProjectItemType.GffFile)
            {
                var filteredNode = new ProjectNode
                {
                    Name = node.Name,
                    ItemType = node.ItemType,
                    Item = node.Item,
                    IsExpanded = node.IsExpanded,
                    Tag = node.Tag,
                    ResourceNumber = node.ResourceNumber
                };

                // Фильтруем дочерние элементы
                foreach (var child in node.Children)
                {
                    var filteredChild = FilterNode(child);
                    if (filteredChild != null)
                    {
                        filteredNode.Children.Add(filteredChild);
                    }
                }

                return filteredNode;
            }
            // Для ресурсов применяем фильтры
            else if (node.ItemType == ProjectItemType.Resource && node.Item is ResourceInfo resourceInfo)
            {
                // Проверяем, должен ли ресурс отображаться в соответствии с фильтрами
                bool shouldShow = resourceInfo.Type switch
                {
                    ResourceType.Image => ShowImages,
                    ResourceType.Palette => ShowPalettes,
                    ResourceType.Font => ShowFonts,
                    ResourceType.Text => ShowTexts,
                    ResourceType.Script => ShowScripts,
                    ResourceType.Sound => ShowSounds,
                    ResourceType.Music => ShowMusic,
                    ResourceType.Dialog => ShowDialogs,
                    ResourceType.Item => ShowItems,
                    ResourceType.Character => ShowCharacters,
                    ResourceType.Map => ShowMaps,
                    ResourceType.Animation => ShowAnimations,
                    ResourceType.RawData => ShowRawData,
                    ResourceType.AnimationControl => ShowAnimationControls,
                    ResourceType.Name => ShowNames,
                    ResourceType.Unknown => ShowUnknown,
                    _ => ShowUnknown // Для всех остальных нераспознанных типов
                };

                return shouldShow ? node : null;
            }

            return node;
        }

        private void ToggleFilter(string filterType)
        {
            switch (filterType)
            {
                case "Images":
                    ShowImages = !ShowImages;
                    break;
                case "Palettes":
                    ShowPalettes = !ShowPalettes;
                    break;
                case "Fonts":
                    ShowFonts = !ShowFonts;
                    break;
                case "Texts":
                    ShowTexts = !ShowTexts;
                    break;
                case "Scripts":
                    ShowScripts = !ShowScripts;
                    break;
                case "Sounds":
                    ShowSounds = !ShowSounds;
                    break;
                case "Music":
                    ShowMusic = !ShowMusic;
                    break;
                case "Dialogs":
                    ShowDialogs = !ShowDialogs;
                    break;
                case "Items":
                    ShowItems = !ShowItems;
                    break;
                case "Characters":
                    ShowCharacters = !ShowCharacters;
                    break;
                case "Maps":
                    ShowMaps = !ShowMaps;
                    break;
                case "Animations":
                    ShowAnimations = !ShowAnimations;
                    break;
                case "RawData":
                    ShowRawData = !ShowRawData;
                    break;
                case "AnimationControls":
                    ShowAnimationControls = !ShowAnimationControls;
                    break;
                case "Unknown":
                    ShowUnknown = !ShowUnknown;
                    break;
                case "Names":
                    ShowNames = !ShowNames;
                    break;
            }
        }

        private void ExpandAll()
        {
            foreach (var node in RootNodes)
            {
                SetNodeExpanded(node, true);
            }
        }

        private void CollapseAll()
        {
            foreach (var node in RootNodes)
            {
                SetNodeExpanded(node, false);
            }
        }

        private void RefreshTree()
        {
            LoadProjectTree();
        }

        private void SetNodeExpanded(ProjectNode node, bool isExpanded)
        {
            node.IsExpanded = isExpanded;
            foreach (var child in node.Children)
            {
                SetNodeExpanded(child, isExpanded);
            }
        }

        public void OpenResource(string tag, int number)
        {
            // Логика открытия ресурса в редакторе
            var resource = FindResource(tag, number);
            string gffFile = $"{tag} - {number}";
            if (resource != null)
            {
                // Открываем ресурс (можно через событие или сервис)
                ResourceOpened?.Invoke(this, new ResourceEventArgs(ProjectAction.ResourceOpened, CurrentProject, resource, gffFile));
            }
        }

        private ResourceInfo FindResource(string tag, int number)
        {
            foreach (var projectNode in RootNodes)
            {
                foreach (var fileNode in projectNode.Children)
                {
                    foreach (var resourceNode in fileNode.Children)
                    {
                        if (resourceNode.Tag == tag && resourceNode.ResourceNumber == number)
                        {
                            return resourceNode.Item as ResourceInfo;
                        }
                    }
                }
            }
            return null;
        }

        private void OnNodeDoubleClicked(ProjectNode node)
        {
            if (node?.ItemType == ProjectItemType.Resource)
            {
                OpenResource(node.Tag, node.ResourceNumber);
            }
        }

        public event EventHandler<ResourceEventArgs> ResourceOpened;

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}