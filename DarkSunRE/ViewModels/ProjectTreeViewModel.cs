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
        private Project _currentProject;

        public ICommand NodeDoubleClickedCommand { get; }
        public ICommand GenerateNewFileCommand { get; }
        public ICommand SaveAllJsonsCommand { get; }
        public ICommand LoadAllJsonsCommand { get; }

        public ProjectTreeViewModel(IProjectService projectService, IStatusService statusService)
        {
            _projectService = projectService;
            _statusService = statusService;

            RootNodes = new ObservableCollection<ProjectNode>();

            // Команды для работы с деревом
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            RefreshCommand = new RelayCommand(RefreshTree);
            NodeDoubleClickedCommand = new RelayCommand<ProjectNode>(OnNodeDoubleClicked);
            GenerateNewFileCommand = new RelayCommand<ProjectNode>(GenerateNewFile);
            SaveAllJsonsCommand = new RelayCommand<ProjectNode>(SaveAllJsons);
            LoadAllJsonsCommand = new RelayCommand<ProjectNode>(LoadAllJsons);
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
                var gff = DSunTools.Common.Gff.GffFile.Create(file.Data);
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
                            updatedBytes = gff.ReplaceResource(resource.Tag, resource.Number, doc.newBytes);
                            gff = DSunTools.Common.Gff.GffFile.Create(updatedBytes); // refresh tables for next replace
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
                _statusService.ShowSuccess("Project tree loaded");
            }
            catch (Exception ex)
            {
                _statusService.ShowError("Failed to load project tree", ex);
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
 