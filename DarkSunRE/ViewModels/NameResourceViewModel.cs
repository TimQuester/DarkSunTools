using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DarkSunRE.Commands;
using DarkSunRE.Models;

namespace DarkSunRE.ViewModels
{
    public class NameResourceViewModel : DocumentViewModel
    {
        private NameResource _nameResource;
        private ObservableCollection<NameEntry> _entries;
        private NameEntry _selectedEntry;
        private ResourceInfo _resourceInfo;
        private List<DSunTools.Common.Gpl.GplTransliterationRule> _transliterationRules = new();
        private string _validationError = string.Empty;

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                _validationError = value;
                OnPropertyChanged();
            }
        }

        public NameResource NameResource
        {
            get => _nameResource;
            set
            {
                _nameResource = value;
                OnPropertyChanged();
                if (_nameResource != null)
                {
                    Entries = new ObservableCollection<NameEntry>(_nameResource.Entries);
                }
            }
        }

        public ObservableCollection<NameEntry> Entries
        {
            get => _entries;
            set
            {
                _entries = value;
                OnPropertyChanged();
            }
        }

        public NameEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                _selectedEntry = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand AddEntryCommand { get; private set; }
        public ICommand RemoveEntryCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }

        public NameResourceViewModel()
        {
            Title = "Name Resource";
            ToolTip = "Name Resource Editor";
            CanClose = true;
            Entries = new ObservableCollection<NameEntry>();
            
            AddEntryCommand = new RelayCommand(AddEntry);
            RemoveEntryCommand = new RelayCommand(RemoveEntry, () => SelectedEntry != null);
            SaveCommand = new RelayCommand(Save);
        }

        public void Initialize(NameResource nameResource, string fileName, ResourceInfo resourceInfo = null, IEnumerable<TransliterationRule>? rules = null)
        {
            NameResource = nameResource;
            _resourceInfo = resourceInfo;
            _transliterationRules = rules?
                .Where(rule => rule != null)
                .Select(rule => new DSunTools.Common.Gpl.GplTransliterationRule(rule.From ?? string.Empty, rule.To ?? string.Empty))
                .ToList() ?? new List<DSunTools.Common.Gpl.GplTransliterationRule>();
            foreach (NameEntry entry in Entries)
                entry.MainName = DSunTools.Common.Gpl.GplTransliteration.DecodeToDisplay(entry.MainName, _transliterationRules);

            File = fileName;
            IsModified = false;
            
            // Устанавливаем заголовок в формате "имя файла: наименование ресурса"
            var parts = fileName.Split(':');
            if (parts.Length == 2)
            {
                Title = $"{parts[0]}: {parts[1]}";
            }
            else
            {
                Title = fileName;
            }
        }

        private void AddEntry()
        {
            var newEntry = new NameEntry
            {
                MainName = "New Entry",
                Path = "",
                Index = 0,
                Flag = 0
            };
            
            Entries.Add(newEntry);
            IsModified = true;
        }

        private void RemoveEntry()
        {
            if (SelectedEntry != null)
            {
                Entries.Remove(SelectedEntry);
                IsModified = true;
            }
        }

        public override void Save()
        {
            if (_nameResource == null)
                return;

            _nameResource.Entries.Clear();
            foreach (var entry in Entries)
                _nameResource.Entries.Add(entry);

            try
            {
                if (_resourceInfo != null)
                {
                    _resourceInfo.ModifiedData = _nameResource.SaveToBytes(_transliterationRules);
                    _resourceInfo.IsModified = true;
                }

                ValidationError = string.Empty;
                IsModified = false;
            }
            catch (InvalidOperationException ex)
            {
                ValidationError = ex.Message;
            }
        }

        public override void Reload()
        {
            if (_nameResource != null)
            {
                Entries = new ObservableCollection<NameEntry>(_nameResource.Entries);
                foreach (NameEntry entry in Entries)
                    entry.MainName = DSunTools.Common.Gpl.GplTransliteration.DecodeToDisplay(entry.MainName, _transliterationRules);
            }

            ValidationError = string.Empty;
            IsModified = false;
        }
    }
}