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
        private bool _realString;
        public int StringNumber { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public string OriginalString { get; set; }
        public byte[] OriginalBytes;
        public byte[] NewBytes;
        public int OriginalSize { get => OriginalString.Length; }
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
        public int NewSize {
            get => _newSize;
            set
            {
                _newSize = value;
                OnPropertyChanged();
            }
        }
        public bool RealString
        {
            get => _realString;
            set
            {
                _realString = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
