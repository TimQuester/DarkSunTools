using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class CreateProjectViewModel : BaseDialogViewModel
    {
        private readonly IGameDefinitionService _gameDefinitionService;
        private readonly IFileService _fileService;
        private readonly IUiService _uiService;
        private readonly IStatusService _statusService;

        private GameDefinition _selectedGame;
        private string _projectName;
        private string _originalGamePath;
        private string _modifiedGamePath;
        private string _jsonFolderPath;

        public event EventHandler<ProjectCreatedEventArgs>? ProjectCreated;

        public CreateProjectViewModel(
            IGameDefinitionService gameDefinitionService,
            IFileService fileService,
            IUiService uiService,
            IStatusService statusService)
        {
            _gameDefinitionService = gameDefinitionService ?? throw new ArgumentNullException(nameof(gameDefinitionService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));

            Title = "Create New Project";
            AvailableGames = _gameDefinitionService.GetSupportedGames();
            SelectedGame = AvailableGames.FirstOrDefault();

            CreateCommand = new RelayCommand(CreateProject);
            CancelCommand = new RelayCommand(Cancel);

            BrowseOriginalGameCommand = new RelayCommand(BrowseOriginalGame);
            BrowseModifiedGameCommand = new RelayCommand(BrowseModifiedGame);
            BrowseJsonFolderCommand = new RelayCommand(BrowseJsonFolder);

            ProjectName = "New project";
            UpdateModifiedGamePath();
        }

        public ObservableCollection<GameDefinition> AvailableGames { get; }

        public GameDefinition SelectedGame
        {
            get => _selectedGame;
            set
            {
                _selectedGame = value;
                OnPropertyChanged();
                UpdateDefaultPaths();
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                _projectName = value;
                OnPropertyChanged();
                UpdateDefaultPaths();
            }
        }

        public string OriginalGamePath
        {
            get => _originalGamePath;
            set
            {
                _originalGamePath = value;
                OnPropertyChanged();
                ValidatePaths();
            }
        }

        public string ModifiedGamePath
        {
            get => _modifiedGamePath;
            set
            {
                _modifiedGamePath = value;
                OnPropertyChanged();
                ValidatePaths();
            }
        }

        public string JsonFolderPath
        {
            get => _jsonFolderPath;
            set
            {
                _jsonFolderPath = value;
                OnPropertyChanged();
            }
        }

        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseOriginalGameCommand { get; }
        public ICommand BrowseModifiedGameCommand { get; }
        public ICommand BrowseJsonFolderCommand { get; }

        private void BrowseOriginalGame()
        {
            var path = _uiService.ShowFolderDialog("Select original game folder");
            if (!string.IsNullOrEmpty(path))
                OriginalGamePath = path;
        }

        private void BrowseModifiedGame()
        {
            var path = _uiService.ShowFolderDialog("Select modified game folder");
            if (!string.IsNullOrEmpty(path))
            {
                ModifiedGamePath = path;
                if (string.IsNullOrEmpty(JsonFolderPath))
                    JsonFolderPath = Path.Combine(ModifiedGamePath, "json");
            }
        }

        private void BrowseJsonFolder()
        {
            var path = _uiService.ShowFolderDialog("Select JSON folder");
            if (!string.IsNullOrEmpty(path))
                JsonFolderPath = path;
        }

        private void UpdateDefaultPaths()
        {
            if (string.IsNullOrEmpty(ProjectName) || SelectedGame == null)
                return;

            ModifiedGamePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DarkSunRE",
                "Projects",
                ProjectName);

            if (string.IsNullOrEmpty(JsonFolderPath))
            {
                JsonFolderPath = Path.Combine(ModifiedGamePath, "json");
            }
        }

        protected override bool CanOk() =>
            !string.IsNullOrEmpty(ProjectName) &&
            !string.IsNullOrEmpty(OriginalGamePath) &&
            !string.IsNullOrEmpty(ModifiedGamePath) &&
            Directory.Exists(OriginalGamePath) &&
            SelectedGame != null;

        protected override void Ok()
        {
            if (!ValidatePaths())
                return;

            base.Ok();
        }

        private bool ValidatePaths()
        {
            if (!Directory.Exists(OriginalGamePath))
            {
                _statusService.ShowError("Original game path does not exist");
                return false;
            }

            // ensure json folder exists or can be created
            if (!string.IsNullOrEmpty(JsonFolderPath) && !_fileService.DirectoryExists(JsonFolderPath))
            {
                try
                {
                    _fileService.CreateDirectory(JsonFolderPath);
                }
                catch (Exception ex)
                {
                    _statusService.ShowError($"Unable to create JSON folder: {ex.Message}", ex);
                    return false;
                }
            }

            return true;
        }

        private void CreateProject()
        {
            try
            {
                _statusService.ShowProgress("Creating project...", 0, true);

                if (!ValidateProjectData())
                    return;

                if (!CreateProjectDirectories())
                    return;

                if (!CopyGameFiles())
                    return;

                var projectData = CreateProjectFile();

                _statusService.ShowSuccess($"Project '{ProjectName}' created successfully!");

                ProjectCreated?.Invoke(this, new ProjectCreatedEventArgs(projectData));

                OnOk(projectData);
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to create project: {ex.Message}", ex);
            }
            finally
            {
                _statusService.Clear();
            }
        }

        private bool ValidateProjectData()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _statusService.ShowError("Project name is required");
                return false;
            }

            if (!_fileService.DirectoryExists(OriginalGamePath))
            {
                _statusService.ShowError("Original game path does not exist");
                return false;
            }

            return true;
        }

        private bool CreateProjectDirectories()
        {
            try
            {
                if (_fileService.DirectoryExists(ModifiedGamePath))
                {
                    var files = _fileService.GetFiles(ModifiedGamePath);
                    if (files.Length > 0)
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"The directory '{ModifiedGamePath}' is not empty. Overwrite existing files?",
                            "Confirm Overwrite",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result != System.Windows.MessageBoxResult.Yes)
                            return false;

                        CreateBackup();
                    }
                }
                else
                {
                    _fileService.CreateDirectory(ModifiedGamePath);
                }

                if (!string.IsNullOrEmpty(JsonFolderPath) && !_fileService.DirectoryExists(JsonFolderPath))
                    _fileService.CreateDirectory(JsonFolderPath);

                return true;
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to create project directories: {ex.Message}", ex);
                return false;
            }
        }

        private bool CopyGameFiles()
        {
            try
            {
                _statusService.ShowProgress("Copying game files...", 50);

                var expectedFiles = _gameDefinitionService.GetExpectedFiles(SelectedGame.Type);
                var copiedFiles = 0;

                foreach (var file in expectedFiles)
                {
                    var sourcePath = _fileService.CombinePaths(OriginalGamePath, file);
                    var destPath = _fileService.CombinePaths(ModifiedGamePath, file);

                    if (_fileService.FileExists(sourcePath))
                    {
                        // Создаем директорию назначения если нужно
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !_fileService.DirectoryExists(destDir))
                        {
                            _fileService.CreateDirectory(destDir);
                        }

                        // Копируем файл
                        var fileData = _fileService.ReadAllBytes(sourcePath);
                        _fileService.WriteAllBytes(destPath, fileData);
                        copiedFiles++;
                    }
                }

                _statusService.ShowMessage($"Copied {copiedFiles} game files");
                return copiedFiles > 0;
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to copy game files: {ex.Message}", ex);
                return false;
            }
        }

        private ProjectCreationData CreateProjectFile()
        {
            _statusService.ShowProgress("Creating project file...", 90);

            var projectData = new ProjectCreationData
            {
                ProjectName = ProjectName,
                OriginalGamePath = OriginalGamePath,
                ModifiedGamePath = ModifiedGamePath,
                JsonFolderPath = JsonFolderPath,
                GameType = SelectedGame.Type,
                CreatedDate = DateTime.Now,
            };

            var projectFilePath = Path.Combine(ModifiedGamePath, $"{ProjectName}.dsproj");
            var projectJson = JsonSerializer.Serialize(projectData, new JsonSerializerOptions { WriteIndented = true });
            _fileService.WriteAllText(projectFilePath, projectJson);

            return projectData;
        }

        private void CreateBackup()
        {
            try
            {
                var backupPath = ModifiedGamePath + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileService.CreateDirectory(backupPath);

                var files = _fileService.GetFiles(ModifiedGamePath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = file.Substring(ModifiedGamePath.Length + 1);
                    var destPath = Path.Combine(backupPath, relativePath);

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !_fileService.DirectoryExists(destDir))
                    {
                        _fileService.CreateDirectory(destDir);
                    }

                    var fileData = _fileService.ReadAllBytes(file);
                    _fileService.WriteAllBytes(destPath, fileData);
                }

                _statusService.ShowMessage($"Backup created at: {backupPath}");
            }
            catch (Exception ex)
            {
                _statusService.ShowWarning($"Failed to create backup: {ex.Message}");
            }
        }

        private string MakeValidFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(ch => !invalidChars.Contains(ch)).ToArray());
        }

        private void UpdateModifiedGamePath()
        {
            if (!string.IsNullOrEmpty(ProjectName) && SelectedGame != null)
            {
                ModifiedGamePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "DarkSunEditor",
                    "Projects",
                    ProjectName);

                if (string.IsNullOrEmpty(JsonFolderPath))
                {
                    JsonFolderPath = Path.Combine(ModifiedGamePath, "json");
                }
            }
        }
    }

    // Event args and DTO used by this VM (kept local)
    public class ProjectCreatedEventArgs : EventArgs
    {
        public ProjectCreationData ProjectData { get; }
        public ProjectCreatedEventArgs(ProjectCreationData data) => ProjectData = data;
    }
}
