using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class CreateProjectViewModel : BaseDialogViewModel
    {
        private readonly IGameDefinitionService _gameDefinitionService;
        private readonly IFileService _fileService;
        private readonly IStatusService _statusService;

        private GameDefinition _selectedGame;
        private string _projectName;
        private string _originalGamePath;
        private string _modifiedGamePath;
        private string _gitRepositoryPath;
        private bool _createGitRepository;
        private bool _isValidGamePath;

        public event EventHandler<ProjectCreatedEventArgs> ProjectCreated;


        public CreateProjectViewModel(
            IGameDefinitionService gameDefinitionService,
            IFileService fileService,
            IStatusService statusService)
        {
            _gameDefinitionService = gameDefinitionService;
            _fileService = fileService;
            _statusService = statusService;

            Title = "Create New Project";
            AvailableGames = _gameDefinitionService.GetSupportedGames();
            SelectedGame = AvailableGames.FirstOrDefault();

            CreateCommand = new RelayCommand(CreateProject, CanCreateProject);
            CancelCommand = new RelayCommand(Cancel);

            BrowseOriginalGameCommand = new RelayCommand(BrowseOriginalGame);
            BrowseModifiedGameCommand = new RelayCommand(BrowseModifiedGame);
            BrowseGitRepositoryCommand = new RelayCommand(BrowseGitRepository);

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

        public string GitRepositoryPath
        {
            get => _gitRepositoryPath;
            set
            {
                _gitRepositoryPath = value;
                OnPropertyChanged();
            }
        }

        public bool CreateGitRepository
        {
            get => _createGitRepository;
            set
            {
                _createGitRepository = value;
                OnPropertyChanged();
                GitRepositoryPath = value ? ModifiedGamePath : null;
            }
        }

        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseOriginalGameCommand { get; }
        public ICommand BrowseModifiedGameCommand { get; }
        public ICommand BrowseGitRepositoryCommand { get; }

        private void BrowseOriginalGame()
        {
            var path = _fileService.ShowFolderDialog("Select original game folder");
            if (!string.IsNullOrEmpty(path))
                OriginalGamePath = path;
        }

        private void BrowseModifiedGame()
        {
            var path = _fileService.ShowFolderDialog("Select modified game folder");
            if (!string.IsNullOrEmpty(path))
                ModifiedGamePath = path;
        }

        private void BrowseGitRepository()
        {
            var path = _fileService.ShowFolderDialog("Select Git repository folder");
            if (!string.IsNullOrEmpty(path))
                GitRepositoryPath = path;
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

            if (CreateGitRepository)
            {
                GitRepositoryPath = ModifiedGamePath;
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
            // Проверка что пути валидны и оригинальная игра существует
            return Directory.Exists(OriginalGamePath);
        }

        private void CreateProject()
        {
            try
            {
                _statusService.ShowProgress("Creating project...", 0, true);

                // 1. Валидация данных
                if (!ValidateProjectData())
                    return;

                // 2. Создание директорий
                if (!CreateProjectDirectories())
                    return;

                // 3. Копирование файлов игры
                if (!CopyGameFiles())
                    return;

                // 4. Создание файла проекта
                var projectData = CreateProjectFile();

                // 5. Успешное завершение
                _statusService.ShowSuccess($"Project '{ProjectName}' created successfully!");

                ProjectCreated?.Invoke(this, new ProjectCreatedEventArgs(projectData));

                // Возвращаем результат
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
        private bool CanCreateProject()
        {
            // Проверяем основные условия
            if (string.IsNullOrWhiteSpace(ProjectName))
                return false;

            if (string.IsNullOrWhiteSpace(OriginalGamePath))
                return false;

            //if (!IsValidGamePath)
            //    return false;

            if (SelectedGame == null)
                return false;

            // Проверяем, не существует ли уже проект с таким именем
            if (_fileService.DirectoryExists(ModifiedGamePath) &&
                _fileService.GetFiles(ModifiedGamePath).Length > 0)
            {
                // Директория не пуста - нужно подтверждение
                return true; // Разрешаем создание, но покажем предупреждение
            }

            return true;
        }
        private void Cancel()
        {
            OnCancel();
        }
        private void ValidateGamePath()
        {
            //if (SelectedGame != null && !string.IsNullOrEmpty(OriginalGamePath))
            //{
            //    IsValidGamePath = _gameValidationService.ValidateGamePath(OriginalGamePath, SelectedGame.Type);

            //    if (IsValidGamePath)
            //    {
            //        var version = _gameValidationService.DetectGameVersion(OriginalGamePath, SelectedGame.Type);
            //        _statusService.ShowMessage($"Detected {SelectedGame.Name} version {version}");
            //    }
            //    else
            //    {
            //        _statusService.ShowWarning("Selected folder doesn't appear to contain the game files");
            //    }
            //}
            //else
            //{
            //    IsValidGamePath = false;
            //}
        }
        private bool ValidateProjectData()
        {
            // Проверка имени проекта
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _statusService.ShowError("Project name is required");
                return false;
            }

            // Проверка пути к оригинальной игре
            if (!_fileService.DirectoryExists(OriginalGamePath))
            {
                _statusService.ShowError("Original game path does not exist");
                return false;
            }

            // Проверка валидности игровых файлов
            //if (!IsValidGamePath)
            //{
            //    _statusService.ShowError("Selected folder doesn't contain valid game files");
            //    return false;
            //}

            return true;
        }
        private bool CreateProjectDirectories()
        {
            try
            {
                // Проверяем, существует ли директория модифицированной игры
                if (_fileService.DirectoryExists(ModifiedGamePath))
                {
                    var files = _fileService.GetFiles(ModifiedGamePath);
                    if (files.Length > 0)
                    {
                        // Директория не пуста - запрашиваем подтверждение
                        var result = MessageBox.Show(
                            $"The directory '{ModifiedGamePath}' is not empty. Overwrite existing files?",
                            "Confirm Overwrite",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                            return false;

                        // Создаем backup или удаляем файлы
                        CreateBackup();
                    }
                }
                else
                {
                    // Создаем новую директорию
                    _fileService.CreateDirectory(ModifiedGamePath);
                }

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
                GitRepositoryPath = CreateGitRepository ? ModifiedGamePath : null,
                GameType = SelectedGame.Type,
                CreatedDate = DateTime.Now,
                //Version = _gameValidationService.DetectGameVersion(OriginalGamePath, SelectedGame.Type)
            };

            // Сохраняем метаданные проекта
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
            }
        }
    }
}
