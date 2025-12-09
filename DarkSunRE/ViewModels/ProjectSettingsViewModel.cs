using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System;
using System.IO;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class ProjectSettingsViewModel : BaseDialogViewModel
    {
        private readonly IUiService _uiService;
        private readonly IFileService _fileService;

        public ProjectSettingsViewModel(IUiService uiService, IFileService fileService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));

            BrowseOriginalCommand = new RelayCommand(BrowseOriginal);
            BrowseModifiedCommand = new RelayCommand(BrowseModified);
            BrowseJsonCommand = new RelayCommand(BrowseJson);

            SaveCommand = new RelayCommand(Save, CanSave);
            CancelCommand = new RelayCommand(Cancel);
        }

        public string OriginalGamePath { get; set; }
        public string ModifiedGamePath { get; set; }
        public string JsonFolderPath { get; set; }

        public ICommand BrowseOriginalCommand { get; }
        public ICommand BrowseModifiedCommand { get; }
        public ICommand BrowseJsonCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public void InitializeFromProject(Project project)
        {
            if (project == null) return;
            OriginalGamePath = project.OriginalGamePath;
            ModifiedGamePath = project.ModifiedGamePath;
            JsonFolderPath = project.JsonFolderPath ?? project.JsonFolderPath; // legacy field may still be present
            OnPropertyChanged(nameof(OriginalGamePath));
            OnPropertyChanged(nameof(ModifiedGamePath));
            OnPropertyChanged(nameof(JsonFolderPath));
        }

        private void BrowseOriginal()
        {
            var path = _uiService.ShowFolderDialog("Select original game folder");
            if (!string.IsNullOrEmpty(path))
                OriginalGamePath = path;
            OnPropertyChanged(nameof(OriginalGamePath));
        }

        private void BrowseModified()
        {
            var path = _uiService.ShowFolderDialog("Select modified game folder");
            if (!string.IsNullOrEmpty(path))
                ModifiedGamePath = path;
            OnPropertyChanged(nameof(ModifiedGamePath));
        }

        private void BrowseJson()
        {
            var path = _uiService.ShowFolderDialog("Select JSON folder");
            if (!string.IsNullOrEmpty(path))
                JsonFolderPath = path;
            OnPropertyChanged(nameof(JsonFolderPath));
        }

        private bool CanSave() => !string.IsNullOrEmpty(ModifiedGamePath);

        private void Save()
        {
            // Return edited paths via DialogResult payload
            OnOk(new { Original = OriginalGamePath, Modified = ModifiedGamePath, Json = JsonFolderPath });
        }

        private void Cancel()
        {
            OnCancel();
        }
    }
}