using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Models
{
    public class GPLString : INotifyPropertyChanged
    {
        private string _newString;
        private int _newSize;
        public int StringNumber { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public string OriginalString { get; set; }
        public string NewString
        {
            get => _newString;
            set
            {
                _newString = value;
                NewSize = _newString != null ? _newString.Length : 0;
                OnPropertyChanged();
            }
        }

        public byte[] OriginalBytes { get; set; }
        public byte[] NewBytes { get; set; }
        public int OriginalSize { get { return OriginalString.Length; } }
        public int NewSize { get { return _newSize; } set { _newSize = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
