using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkSunRE.Models;

namespace DarkSunRE.ViewModels
{
    public class GplDocumentViewModel : DocumentViewModel
    {
        private readonly GPLDocument _document;
        private GPLString _selectedString;
        private readonly List<GplValidationIssue> _validationIssues = new();
        private string _validationSummary = string.Empty;

        private class GplJsonFile
        {
            public string FileName { get; set; }
            public string ResourceName { get; set; }
            public List<GPLString> Strings { get; set; }
        }

        public GplDocumentViewModel(GPLDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            Title = document.Name;
            File = document.Path;
            ToolTip = document.Path;
            CanClose = true;

            Strings = new ObservableCollection<GPLString>(document.decodedStrings ?? new List<GPLString>());
            AttachStrings();
            RefreshValidation();
        }

        public ObservableCollection<GPLString> Strings { get; }

        public GPLString SelectedString
        {
            get => _selectedString;
            set
            {
                if (_selectedString == value) return;
                _selectedString = value;
                OnPropertyChanged();
            }
        }

        public byte[] OriginalBytes => _document.originalBytes;
        public byte[] NewBytes => _document.newBytes;
        public string DocumentPath => _document.Path;
        public string FileName => _document.FileName;
        public string ResourceName => _document.ResourceName;

        public bool ParseAligned => _document.ParseAligned;
        public string ParseError => _document.ParseError ?? string.Empty;

        public bool HasValidationErrors => _validationIssues.Count > 0;
        public int ValidationErrorCount => _validationIssues.Count;
        public string ValidationSummary => _validationSummary;
        public bool CanSave => !HasValidationErrors;

        private void AttachStrings()
        {
            foreach (GPLString str in Strings)
            {
                if (str != null)
                    str.PropertyChanged += OnStringPropertyChanged;
            }
        }

        private void DetachStrings()
        {
            foreach (GPLString str in Strings)
            {
                if (str != null)
                    str.PropertyChanged -= OnStringPropertyChanged;
            }
        }

        private void OnStringPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GPLString.NewString))
                RefreshValidation();
        }

        /// <summary>
        /// Re-run the same validation the encoder uses and push per-string
        /// messages back to the grid. Called on load and after every edit.
        /// </summary>
        public void RefreshValidation()
        {
            _document.decodedStrings = Strings.Where(s => s != null).ToList();

            _validationIssues.Clear();
            _validationIssues.AddRange(_document.Validate());

            var messagesByString = _validationIssues
                .Where(i => i.StringNumber >= 0)
                .GroupBy(i => i.StringNumber)
                .ToDictionary(g => g.Key, g => g.First().Message);

            foreach (GPLString str in Strings)
            {
                if (str == null) continue;
                str.ValidationError = messagesByString.TryGetValue(str.StringNumber, out string message)
                    ? message
                    : null;
            }

            _validationSummary = BuildValidationSummary();

            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(ValidationErrorCount));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(CanSave));
        }

        private string BuildValidationSummary()
        {
            if (_validationIssues.Count == 0)
                return string.Empty;

            var lines = new List<string>();
            var stringIssues = _validationIssues.Where(i => i.StringNumber >= 0).ToList();

            if (!_document.ParseAligned)
            {
                lines.Add($"Parser warning: {_document.ParseError ?? "the chunk was not parsed to the end"}. " +
                          "Strings after the failure point may be missing.");
            }

            if (stringIssues.Count > 0)
            {
                lines.Add($"{stringIssues.Count} string{(stringIssues.Count == 1 ? "" : "s")} cannot be written:");
                foreach (GplValidationIssue issue in stringIssues.Take(3))
                    lines.Add($"#{issue.StringNumber}: {issue.Message}");
                if (stringIssues.Count > 3)
                    lines.Add($"... and {stringIssues.Count - 3} more.");
            }

            lines.Add("File generation is blocked until these errors are fixed.");
            return string.Join(Environment.NewLine, lines);
        }

        public override void Reload()
        {
            DetachStrings();
            Strings.Clear();
            foreach (var str in _document.decodedStrings)
            {
                Strings.Add(str);
            }
            AttachStrings();
            OnPropertyChanged(nameof(Strings));
            RefreshValidation();
            IsModified = false;
        }

        public override void Save()
        {
            ApplyStringChanges();
            SaveModifiedFile();
        }

        public void ApplyStringChanges()
        {
            _document.decodedStrings = Strings.Where(s => s != null).ToList();
            RefreshValidation();

            if (HasValidationErrors)
                throw new InvalidOperationException(ValidationSummary);

            _document.ApplyStringChanges();
            IsModified = true;
            OnPropertyChanged(nameof(NewBytes));
            OnPropertyChanged(nameof(CanSave));
        }

        public void SaveModifiedFile(string targetPath = null)
        {
            var path = targetPath ?? $"{_document.Path}_modified";
            System.IO.File.WriteAllBytes(path, _document.newBytes);
            IsModified = false;
        }

        public void SaveJson(string jsonFolderPath)
        {
            if (string.IsNullOrWhiteSpace(jsonFolderPath))
            {
                throw new InvalidOperationException("JSON folder path is not set.");
            }

            Directory.CreateDirectory(jsonFolderPath);
            var fileName = $"{_document.FileName}_{_document.ResourceName}.json";
            var fullPath = Path.Combine(jsonFolderPath, fileName);

            using var fs = new FileStream(fullPath, FileMode.Create);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var payload = new GplJsonFile
            {
                FileName = _document.FileName,
                ResourceName = _document.ResourceName,
                Strings = Strings.ToList()
            };
            JsonSerializer.Serialize(fs, payload, options);
        }

        public void LoadJson(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("JSON file not found", filePath);
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            List<GPLString> decodedStrings = null;

            // try new format first
            try
            {
                var jsonFile = JsonSerializer.Deserialize<GplJsonFile>(fs);
                if (jsonFile != null)
                {
                    decodedStrings = jsonFile.Strings;
                }
            }
            catch
            {
                // ignore and try legacy
            }

            // If not new format, rewind and try legacy list
            if (decodedStrings == null)
            {
                fs.Position = 0;
                decodedStrings = JsonSerializer.Deserialize<List<GPLString>>(fs);
            }

            if (decodedStrings == null)
            {
                throw new InvalidOperationException("Failed to deserialize GPL strings.");
            }

            DetachStrings();
            Strings.Clear();
            foreach (var s in decodedStrings)
            {
                if (s != null)
                    Strings.Add(s);
            }
            AttachStrings();

            _document.decodedStrings = Strings.ToList();

            // A translation file may legitimately contain errors while it is
            // being edited. Keep the strings loaded so the user can fix them;
            // RefreshValidation below turns those errors into UI messages.
            try
            {
                _document.RefreshNewBytesAfterLoadJson();
            }
            catch (InvalidOperationException)
            {
                // Validation reports the concrete strings.
            }

            RefreshValidation();
            OnPropertyChanged(nameof(NewBytes));
            IsModified = true;
        }
    }
}
