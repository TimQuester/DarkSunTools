using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using System;
using System.Collections.Generic;
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
        public string TransliterationTableText { get; set; }

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
            TransliterationTableText = BuildTableText(project.TransliterationRules);
            OnPropertyChanged(nameof(OriginalGamePath));
            OnPropertyChanged(nameof(ModifiedGamePath));
            OnPropertyChanged(nameof(JsonFolderPath));
            OnPropertyChanged(nameof(TransliterationTableText));
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
            OnOk(new { Original = OriginalGamePath, Modified = ModifiedGamePath, Json = JsonFolderPath, TransliterationRules = ParseTable(TransliterationTableText) });
        }

        private void Cancel()
        {
            OnCancel();
        }

        private static string BuildTableText(IEnumerable<TransliterationRule> rules)
        {
            if (rules == null) return string.Empty;
            return string.Join(";", rules.Select(r => $"{r.From}={r.To}"));
        }

        private static List<TransliterationRule> ParseTable(string text)
        {
            var result = new List<TransliterationRule>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var segments = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                var line = segment.Trim();
                if (line.Length == 0) continue;
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    result.Add(new TransliterationRule { From = parts[0], To = parts[1] });
                }
            }
            return result;
        }

        public List<TransliterationRule> GetTransliterationRules() => ParseTable(TransliterationTableText);
    }
}