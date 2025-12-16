using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        private bool _showUnknown = true;

        public ICommand NodeDoubleClickedCommand { get; }
        public ICommand GenerateNewFileCommand { get; }
        public ICommand SaveAllJsonsCommand { get; }
        public ICommand LoadAllJsonsCommand { get; }
        public ICommand ToggleFilterCommand { get; }

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
            ToggleFilterCommand = new RelayCommand<string>(ToggleFilter);
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

            if (node?.Item is not ResourceFile file || file.Resources == null)
            {
                _statusService.ShowError("Invalid file selection.");
                return;
            }

            Directory.CreateDirectory(CurrentProject.JsonFolderPath);

            foreach (var resource in file.Resources)
            {
                var doc = CurrentProject.OpenedDocuments.FirstOrDefault(d =>
                    string.Equals(d.FileName, file.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.ResourceName, $"{resource.Tag}-{resource.Number}", StringComparison.OrdinalIgnoreCase));

                if (doc != null)
                {
                    var vm = new GplDocumentViewModel(doc);
                    vm.SaveJson(CurrentProject.JsonFolderPath);
                }
            }

            _statusService.ShowSuccess("All JSONs saved.");
        }

        private void LoadAllJsons(ProjectNode node)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(CurrentProject.JsonFolderPath))
            {
                _statusService.ShowError("JSON folder path is not configured.");
                return;
            }

            if (node?.Item is not ResourceFile file || file.Resources == null)
            {
                _statusService.ShowError("Invalid file selection.");
                return;
            }

            foreach (var resource in file.Resources)
            {
                var doc = CurrentProject.OpenedDocuments.FirstOrDefault(d =>
                    string.Equals(d.FileName, file.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.ResourceName, $"{resource.Tag}-{resource.Number}", StringComparison.OrdinalIgnoreCase));

                if (doc != null)
                {
                    var path = Path.Combine(CurrentProject.JsonFolderPath, $"{doc.FileName}_{doc.ResourceName}.json");
                    if (File.Exists(path))
                    {
                        var vm = new GplDocumentViewModel(doc);
                        vm.LoadJson(path);
                    }
                }
            }

            _statusService.ShowSuccess("All JSONs loaded.");
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

                        if (doc != null)
                        {
                            doc.TransliterationRules = CurrentProject.TransliterationRules ?? new List<TransliterationRule>();
                            doc.ApplyStringChanges();
                            updatedBytes = _resourceService.ReplaceResource(gff, resource.Tag, resource.Number, doc.newBytes);
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