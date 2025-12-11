using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public override void Reload()
        {
            Strings.Clear();
            foreach (var str in _document.decodedStrings)
            {
                Strings.Add(str);
            }
            OnPropertyChanged(nameof(Strings));
            IsModified = false;
        }

        public override void Save()
        {
            ApplyStringChanges();
            SaveModifiedFile();
        }

        public void ApplyStringChanges()
        {
            _document.decodedStrings = Strings.ToList();
            _document.ApplyStringChanges();
            IsModified = true;
            OnPropertyChanged(nameof(NewBytes));
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

            Strings.Clear();
            foreach (var s in decodedStrings)
            {
                Strings.Add(s);
            }

            _document.decodedStrings = Strings.ToList();
            _document.RefreshNewBytesAfterLoadJson();
            OnPropertyChanged(nameof(NewBytes));
            IsModified = true;
        }
    }
}

