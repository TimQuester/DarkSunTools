using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.ViewModels
{
    public abstract class DocumentViewModel : INotifyPropertyChanged
    {
        private string _title;
        private string _file;
        private bool _isModified;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        public string File
        {
            get => _file;
            set
            {
                _file = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        public string DisplayTitle => IsModified ? $"{Title}: {File} *" : $"{Title}: {File}";


        public string ToolTip { get; set; }
        public bool CanClose { get; set; }
        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified == value) return;
                _isModified = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public abstract void Save();
        public abstract void Reload();

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
