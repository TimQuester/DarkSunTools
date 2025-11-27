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
        public string Title { get; set; }
        public string ToolTip { get; set; }
        public bool CanClose { get; set; }
        public bool IsModified { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public abstract void Save();
        public abstract void Reload();

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
