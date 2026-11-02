using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using DarkSunRE.Commands;
using DarkSunRE.Models;
using DSunTools.Common.Gpl;

namespace DarkSunRE.ViewModels
{
    public class TextResourceViewModel : DocumentViewModel
    {
        private ResourceInfo _resourceInfo;
        private byte[] _originalBytes = Array.Empty<byte>();
        private List<GplTransliterationRule> _transliterationRules = new();
        private string _text = string.Empty;
        private string _validationError = string.Empty;

        public TextResourceViewModel()
        {
            Title = "Text Resource";
            CanClose = true;
            SaveCommand = new RelayCommand(Save, () => IsModified && string.IsNullOrEmpty(ValidationError));
            ReloadCommand = new RelayCommand(Reload);
        }

        public string OriginalText { get; private set; } = string.Empty;

        public string Text
        {
            get => _text;
            set
            {
                if (string.Equals(_text, value, StringComparison.Ordinal))
                    return;

                _text = value ?? string.Empty;
                IsModified = true;
                ValidateText();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSave));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                if (string.Equals(_validationError, value, StringComparison.Ordinal))
                    return;

                _validationError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSave));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanSave => IsModified && string.IsNullOrEmpty(ValidationError);
        public ICommand SaveCommand { get; }
        public ICommand ReloadCommand { get; }

        public void Initialize(ResourceInfo resourceInfo, byte[] data, IEnumerable<TransliterationRule>? rules)
        {
            _resourceInfo = resourceInfo ?? throw new ArgumentNullException(nameof(resourceInfo));
            _originalBytes = data == null ? Array.Empty<byte>() : (byte[])data.Clone();
            _transliterationRules = rules?
                .Where(rule => rule != null)
                .Select(rule => new GplTransliterationRule(rule.From ?? string.Empty, rule.To ?? string.Empty))
                .ToList() ?? new List<GplTransliterationRule>();

            Title = $"{resourceInfo.Tag}-{resourceInfo.Number}";
            File = resourceInfo.Tag;
            OriginalText = DecodeForDisplay(_originalBytes);
            _text = OriginalText;
            IsModified = false;
            ValidationError = string.Empty;
            OnPropertyChanged(nameof(OriginalText));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(CanSave));
        }

        public override void Save()
        {
            if (_resourceInfo == null)
                throw new InvalidOperationException("The text resource has not been initialized.");

            ValidateText();
            if (!string.IsNullOrEmpty(ValidationError))
                return;

            string gameText = GplTransliteration.EncodeToGameCharset(Text, _transliterationRules);
            _resourceInfo.ModifiedData = Encoding.Latin1.GetBytes(gameText);
            _resourceInfo.IsModified = true;
            _originalBytes = (byte[])_resourceInfo.ModifiedData.Clone();
            OriginalText = Text;
            IsModified = false;
            OnPropertyChanged(nameof(OriginalText));
            OnPropertyChanged(nameof(CanSave));
        }

        public override void Reload()
        {
            if (_resourceInfo == null)
                return;

            _text = OriginalText = DecodeForDisplay(_originalBytes);
            IsModified = false;
            ValidationError = string.Empty;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(OriginalText));
            OnPropertyChanged(nameof(CanSave));
        }

        private string DecodeForDisplay(byte[] bytes)
        {
            string gameText = Encoding.Latin1.GetString(bytes);
            return GplTransliteration.DecodeToDisplay(gameText, _transliterationRules);
        }

        private void ValidateText()
        {
            string gameText = GplTransliteration.EncodeToGameCharset(Text, _transliterationRules);
            char invalidCharacter = gameText.FirstOrDefault(character => character > byte.MaxValue);
            if (invalidCharacter > byte.MaxValue)
            {
                ValidationError = $"Character U+{(int)invalidCharacter:X4} '{invalidCharacter}' has no one-byte game glyph mapping. Add a transliteration rule.";
                return;
            }

            ValidationError = string.Empty;
        }
    }
}
