using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly IStatusService _statusService;
        private readonly IFileService _fileService;      

        private Project _currentProject;

        public bool IsBusy { get; private set; }
        public ProjectTreeViewModel ProjectTree { get; }

        private bool IsProjectModified = false;
        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand ExitCommand { get; }

        public bool IsModified;

        //private bool CanCreateProject()
        //{
        //    return !string.IsNullOrEmpty(ProjectName) &&
        //           !string.IsNullOrEmpty(OriginalGamePath) &&
        //           !string.IsNullOrEmpty(ModifiedGamePath) &&
        //           _fileService.DirectoryExists(OriginalGamePath);
        //}
        //public ObservableCollection<RecentProject> RecentProjects { get; }

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
            //_statusService.StatusChanged += OnStatusChanged;

            ProjectTree.ResourceOpened += OnResourceOpened;


            NewProjectCommand = new RelayCommand(ExecuteNewProject, CanExecuteNewProject);
            OpenProjectCommand = new RelayCommand(ExecuteOpenProject);
            SaveProjectCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            ExitCommand = new RelayCommand(ExecuteExit);

            //RecentProjects = new ObservableCollection<RecentProject>();

            //LoadRecentProjects();
        }

        private void OnResourceChanged(object? sender, Services.ResourceEventArgs e)
        {
            throw new NotImplementedException();
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
                    break;

                case ProjectAction.Modified:
                    IsProjectModified = true;
                    _statusService.ShowMessage("Project modified", StatusMessageType.Info);
                    break;

                case ProjectAction.Saved:
                    IsProjectModified = false;
                    _statusService.ShowMessage("Project saved", StatusMessageType.Info);
                    break;
            }
        }
        private void OnResourceChanged(object sender, ResourceEventArgs e)
        {
            // Обновление UI при изменении ресурсов
            if (e.Action == ProjectAction.ResourceModified)
            {
                // Обновить дерево проекта
                // Показать уведомление и т.д.
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

        //private void AddToRecentProjects(Project project)
        //{
        //    var recentProject = new RecentProject
        //    {
        //        Name = project.ProjectName,
        //        Path = project.ProjectFilePath,
        //        GameType = project.GameType,
        //        LastAccessed = DateTime.Now
        //    };

        //    RecentProjects.Insert(0, recentProject);

        //    // Сохраняем в настройках
        //    SaveRecentProjects();
        //}

        //private void LoadRecentProjects()
        //{
        //    // Загрузка из настроек/файла
        //    var recent = _settingsService.GetRecentProjects();
        //    foreach (var project in recent)
        //    {
        //        RecentProjects.Add(project);
        //    }
        //}

        //private void SaveRecentProjects()
        //{
        //    _settingsService.SaveRecentProjects(RecentProjects.ToList());
        //}
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
                    e.ProjectData.GitRepositoryPath,
                    e.ProjectData.GameType);

                CurrentProject = project;
                _statusService.ShowSuccess($"Project '{project.ProjectName}' created!");
            };

            _dialogService.ShowDialog(createProjectVm);
        }
        private bool CanExecuteNewProject() => !IsBusy;

        private void ExecuteOpenProject()
        {
            var filePath = _dialogService.ShowOpenFileDialog("Project files|*.dsproj");
            if (!string.IsNullOrEmpty(filePath))
            {
                // Логика открытия проекта
            }
        }

        private void ExecuteSave()
        {
            // Логика сохранения
        }

        private bool CanExecuteSave() => CurrentProject != null && IsModified;

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
            var reArgs = new ResourceEventArgs(ProjectAction.ResourceReplaced, project, resource, gffFileName);            
            OnResourceChanged(this, reArgs);
            var pjArgs = new ProjectEventArgs(ProjectAction.Modified, project);
            OnProjectChanged(this, pjArgs);
        }
    }
}
