using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DarkSunRE.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly IStatusService _statusService;
        private readonly IFileService _fileService;      

        private Project _currentProject;
        private CancellationTokenSource _operationCts;

        public bool IsBusy { get; private set; }
        public ProjectTreeViewModel ProjectTree { get; }

        private bool IsProjectModified = false;
        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand CancelOperationCommand { get; }
        public ICommand SettingsCommand { get; }

        public bool IsModified;

        public MainViewModel(IProjectService projectService, IDialogService dialogService, IStatusService statusService, IFileService fileService, ProjectTreeViewModel projectTreeViewModel)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _statusService = statusService;
            _fileService = fileService;
            ProjectTree = projectTreeViewModel;

            // Подписка на события
            _projectService.ProjectChanged += OnProjectChanged;
            _projectService.ResourceChanged += OnResourceChanged;
            _projectService.GffFileChanged += OnGffFileChanged;

            ProjectTree.ResourceOpened += OnResourceOpened;

            NewProjectCommand = new RelayCommand(ExecuteNewProject, CanExecuteNewProject);
            OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, () => !IsBusy);
            SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync, () => CurrentProject != null && IsModified);
            CancelOperationCommand = new RelayCommand(CancelOperation, () => IsBusy);
            ExitCommand = new RelayCommand(ExecuteExit);
            SettingsCommand = new RelayCommand(OpenSettings);
        }

        // Implemented to handle events coming from services (ResourceChanged)
        private void OnResourceChanged(object? sender, Services.ResourceEventArgs e)
        {
            // Update UI/state in response to service-level resource events
            if (e == null) return;

            // If the event concerns the currently opened project, refresh tree or update nodes
            if (e.Project != null && e.Project == CurrentProject)
            {
                // Reassign CurrentProject to trigger ProjectTree load, or call RefreshCommand
                ProjectTree.CurrentProject = e.Project;
            }

            // Show short info in status bar depending on action
            switch (e.Action)
            {
                case ProjectAction.ResourceReplaced:
                    _statusService?.ShowMessage($"Resource {e.GffFileName}:{e.Resource?.Tag}-{e.Resource?.Number} replaced", StatusMessageType.Info);
                    break;
                case ProjectAction.ResourceModified:
                    _statusService?.ShowMessage("Resource modified", StatusMessageType.Info);
                    break;
                case ProjectAction.ResourceOpened:
                    _statusService?.ShowMessage("Resource opened", StatusMessageType.Info);
                    break;
                default:
                    _statusService?.ShowMessage("Project resources updated", StatusMessageType.Info);
                    break;
            }
        }

        public Project CurrentProject
        {
            get => _currentProject;
            set
            {
                SetProperty(ref _currentProject, value);
                ProjectTree.CurrentProject = value;
            }                
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnProjectChanged(object sender, ProjectEventArgs e)
        {
            switch (e.Action)
            {
                case ProjectAction.Created:
                    _statusService.ShowMessage($"Project created: {e.Project.ProjectName}", StatusMessageType.Info);
                    _statusService.SetProjectPath(e.Project.ModifiedGamePath ?? e.Project.OriginalGamePath);
                    break;

                case ProjectAction.Opened:
                    _statusService.ShowMessage($"Project opened: {e.Project.ProjectName}", StatusMessageType.Info);
                    _statusService.SetProjectPath(e.Project.ModifiedGamePath ?? e.Project.OriginalGamePath);
                    break;

                case ProjectAction.Saved:
                    IsProjectModified = false;
                    _statusService.ShowMessage("Project saved", StatusMessageType.Info);
                    _statusService.SetProjectPath(e.Project.ModifiedGamePath ?? e.Project.OriginalGamePath);
                    break;

                case ProjectAction.Modified:
                    IsProjectModified = true;
                    _statusService.ShowMessage("Project modified", StatusMessageType.Info);
                    break;
            }
        }

        private void OnGffFileChanged(object sender, GffFileEventArgs e)
        {
            // Обработка изменений GFF файлов
        }

        private void OnStatusChanged(object sender, GffFileEventArgs e)
        {
            // Обработка изменений статусов
        } 

        private bool ValidateProjectParameters(CreateProjectViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.ProjectName))
            {
                _statusService.ShowError("Project name is required");
                return false;
            }

            if (!_fileService.DirectoryExists(vm.OriginalGamePath))
            {
                _statusService.ShowError("Original game path does not exist");
                return false;
            }

            return true;
        }

        public ObservableCollection<DocumentViewModel> Documents { get; set; }        
        public CommandManager CommandManager { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void ExecuteNewProject()
        {
            var createProjectVm = _dialogService.CreateViewModel<CreateProjectViewModel>();

            createProjectVm.ProjectCreated += (sender, e) =>
            {
                // Используем данные из события
                var project = _projectService.CreateNewProject(
                    e.ProjectData.OriginalGamePath,
                    e.ProjectData.ModifiedGamePath,
                    e.ProjectData.JsonFolderPath,
                    e.ProjectData.GameType);

                CurrentProject = project;
                _statusService.ShowSuccess($"Project '{project.ProjectName}' created!");
            };

            _dialogService.ShowDialog(createProjectVm);
        }
        private bool CanExecuteNewProject() => !IsBusy;

        private async Task OpenProjectAsync()
        {
            var filePath = _dialogService.ShowOpenFileDialog("Project files|*.dsproj");
            if (string.IsNullOrEmpty(filePath))
                return;

            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();

            try
            {
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));
                _statusService.ShowProgress("Opening project...", 0, true);

                var progress = new Progress<StatusMessage>(s =>
                {
                    _statusService.ShowProgress(s.Message, s.Progress ?? 0, s.IsIndeterminate);
                });
                
                var project = await _projectService.LoadProjectAsync(filePath, progress, _operationCts.Token);

                if (project == null)
                {
                    _statusService.ShowError($"Failed to open project: {Path.GetFileName(filePath)}");
                    return;
                }
                
                CurrentProject = project;
                IsModified = false;
                OnPropertyChanged(nameof(IsModified));
                _statusService.ShowSuccess($"Project '{project.ProjectName ?? Path.GetFileNameWithoutExtension(filePath)}' opened.");
            }
            catch (OperationCanceledException)
            {
                _statusService.ShowMessage("Open cancelled");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to open project: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
                _statusService.Clear();
                _operationCts = null;
            }
        }

        private async Task SaveProjectAsync()
        {
            if (CurrentProject == null)
                return;

            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();

            try
            {
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));
                
                await _projectService.SaveProjectAsync(CurrentProject);

                _statusService.ShowSuccess($"Project '{CurrentProject.ProjectName}' saved.");
                IsModified = false;
                OnPropertyChanged(nameof(IsModified));
            }
            catch (OperationCanceledException)
            {
                _statusService.ShowMessage("Save cancelled");
            }
            catch (Exception ex)
            {
                _statusService.ShowError($"Failed to save project: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
                _operationCts = null;
            }
        }

        private void CancelOperation()
        {
            _operationCts?.Cancel();
        }

        private void ExecuteExit()
        {
            ;
        }

        private void OnResourceOpened(object sender, ResourceEventArgs e)
        {
            // Обработка открытия ресурса
            //OpenResourceEditor(e.Resource);
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }      

        public void ReplaceResource(Project project, string gffFileName, ResourceInfo resource, byte[] newData)
        {            
            var reArgs = new Services.ResourceEventArgs(ProjectAction.ResourceReplaced, project, resource, gffFileName);            
            OnResourceChanged(this, reArgs);
            var pjArgs = new ProjectEventArgs(ProjectAction.Modified, project);
            OnProjectChanged(this, pjArgs);
        }

        private void OpenSettings()
        {
            var vm = _dialogService.CreateViewModel<ProjectSettingsViewModel>();
            vm.InitializeFromProject(CurrentProject);
            var result = _dialogService.ShowDialog(vm);
            if (result == true)
            {                
                if (vm.DialogResult == true)                
                    if (CurrentProject != null)
                    {
                        CurrentProject.OriginalGamePath = vm.OriginalGamePath;
                        CurrentProject.ModifiedGamePath = vm.ModifiedGamePath;                        
                        CurrentProject.JsonFolderPath = vm.JsonFolderPath;
                        CurrentProject.TransliterationRules = vm.GetTransliterationRules();
                        _statusService.SetProjectPath(CurrentProject.ModifiedGamePath ?? CurrentProject.OriginalGamePath);                        
                        _projectService.SaveProject(CurrentProject);
                    }                
            }
        }
    }
}
