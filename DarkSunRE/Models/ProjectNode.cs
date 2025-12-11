using DarkSunRE.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Models
{
    public class ProjectNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string Name { get; set; }
        public ProjectItemType ItemType { get; set; }
        public object Item { get; set; } // Project, GffFileInfo или ResourceInfo
        public string Tag { get; set; }
        public int ResourceNumber { get; set; }
        public ObservableCollection<ProjectNode> Children { get; set; } = new ObservableCollection<ProjectNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ImageSource Icon
        {
            get
            {
                var iconName = ItemType switch
                {
                    ProjectItemType.Project => "project",
                    ProjectItemType.GffFile => "file",
                    ProjectItemType.Resource => "resource",
                    _ => "default"
                };
                return GetImageSource($"{iconName}.png");
            }
        }

        private ImageSource GetImageSource(string imageName)
        {
            // Логика загрузки иконок
            return null; // Заглушка
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
