using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DSunTools.Common.Gpl;

namespace DarkSunRE.Models
{
    public class GPLString : INotifyPropertyChanged
    {
        private string _newString;
        private int _newSize;
        private string _validationError;

        public int StringNumber { get; set; }

        /// <summary>First payload byte (the byte after the 0x05 marker).</summary>
        public int StartPosition { get; set; }

        public int EndPosition { get; set; }

        /// <summary>Number of payload bytes, through the terminator.</summary>
        public int EncodedLength { get; set; }

        public GplStringSubType SubType { get; set; } = GplStringSubType.Compressed;

        public string OriginalString { get; set; }

        public byte[] OriginalBytes;
        public byte[] NewBytes;

        public int OriginalSize => OriginalString?.Length ?? 0;

        /// <summary>True for strings the fixed-length editor can rewrite.</summary>
        public bool IsEditable => SubType == GplStringSubType.Compressed;

        /// <summary>Maximum number of game characters this span can hold.</summary>
        public int MaxLength => OriginalSize;

        public bool IsOverlong => NewString != null && NewSize > MaxLength;

        [JsonIgnore]
        public bool IsUnderlong => NewString != null && NewSize < MaxLength;

        [JsonIgnore]
        public bool HasLengthMismatch => NewString != null && NewSize != MaxLength;

        /// <summary>Current and maximum character counts for the editor.</summary>
        [JsonIgnore]
        public string LengthDisplay => $"{NewSize}/{MaxLength}";

        /// <summary>
        /// Per-string validation message produced by the document. Null when
        /// the string can be encoded without losing the fixed-length layout.
        /// </summary>
        [JsonIgnore]
        public string ValidationError
        {
            get => _validationError;
            set
            {
                if (string.Equals(_validationError, value, StringComparison.Ordinal)) return;
                _validationError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationError));
            }
        }

        [JsonIgnore]
        public bool HasValidationError => !string.IsNullOrEmpty(_validationError);

        public string NewString
        {
            get => _newString;
            set
            {
                _newString = value;
                NewSize = _newString != null ? _newString.Length : 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverlong));
                OnPropertyChanged(nameof(IsUnderlong));
                OnPropertyChanged(nameof(HasLengthMismatch));
            }
        }

        public int NewSize
        {
            get => _newSize;
            set
            {
                if (_newSize == value) return;
                _newSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LengthDisplay));
                OnPropertyChanged(nameof(IsOverlong));
                OnPropertyChanged(nameof(IsUnderlong));
                OnPropertyChanged(nameof(HasLengthMismatch));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
