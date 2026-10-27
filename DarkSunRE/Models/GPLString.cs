using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DSunTools.Common.Gpl;

namespace DarkSunRE.Models
{
    public class GPLString : INotifyPropertyChanged
    {
        private string _newString;
        private int _newSize;
        private bool _realString;

        public int StringNumber { get; set; }

        /// <summary>First payload byte (the byte after the 0x05 marker).</summary>
        public int StartPosition { get; set; }

        public int EndPosition { get; set; }

        /// <summary>Number of payload bytes, including terminator/padding.</summary>
        public int EncodedLength { get; set; }

        public GplStringSubType SubType { get; set; } = GplStringSubType.Compressed;

        public string OriginalString { get; set; }

        public byte[] OriginalBytes;
        public byte[] NewBytes;

        public int OriginalSize => OriginalString?.Length ?? 0;

        /// <summary>True for strings the fixed-length editor can rewrite.</summary>
        public bool IsEditable => SubType == GplStringSubType.Compressed;

        /// <summary>Maximum number of characters this span can hold.</summary>
        public int MaxLength => OriginalSize;

        public bool IsOverlong => NewString != null && NewString.Length > MaxLength;

        public string NewString
        {
            get => _newString;
            set
            {
                _newString = value;
                NewSize = _newString != null ? _newString.Length : 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverlong));
            }
        }

        public int NewSize
        {
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
