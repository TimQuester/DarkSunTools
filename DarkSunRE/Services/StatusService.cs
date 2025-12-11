using DarkSunRE.Commands;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace DarkSunRE.Services
{
    public class StatusService : IStatusService, INotifyPropertyChanged
    {
        private StatusMessage _currentMessage;
        private readonly ObservableCollection<StatusMessage> _history = new ObservableCollection<StatusMessage>();
        private string _projectPath;
        private string _jsonFolder;

        // track last operation outcome
        private string _lastOutcomeMessage;
        private StatusMessageType _lastOutcomeType = StatusMessageType.Info;

        public StatusService()
        {
            ClearCommand = new RelayCommand(Clear);
            ShowHistoryCommand = new RelayCommand(ShowHistory);
        }

        public IReadOnlyCollection<StatusMessage> MessageHistory => _history;

        public StatusMessage CurrentMessage
        {
            get => _currentMessage;
            private set
            {
                if (_currentMessage == value) return;
                var previous = _currentMessage;
                _currentMessage = value;
                RaiseAllMessagePropertiesChanged(previous);
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(_currentMessage, previous));
            }
        }

        public string ProjectPath
        {
            get => _projectPath;
            private set
            {
                if (_projectPath == value) return;
                _projectPath = value;
                OnPropertyChanged(nameof(ProjectPath));
            }
        }

        public string JsonFolder
        {
            get => _jsonFolder;
            private set
            {
                if (_jsonFolder == value) return;
                _jsonFolder = value;
                OnPropertyChanged(nameof(JsonFolder));
            }
        }

        // Bindable helpers used by StatusBarView
        public string Message => CurrentMessage?.Message ?? string.Empty;
        public StatusMessageType MessageType => CurrentMessage?.Type ?? StatusMessageType.Info;
        public double Progress => CurrentMessage?.Progress ?? 0.0;
        public bool IsIndeterminate => CurrentMessage?.IsIndeterminate ?? false;
        public bool IsVisible => true; // statusbar always visible; content depends on Message

        public ICommand ClearCommand { get; }
        public ICommand ShowHistoryCommand { get; }

        public event EventHandler<StatusChangedEventArgs> StatusChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void ShowMessage(string message, StatusMessageType type = StatusMessageType.Info, TimeSpan? timeout = null)
        {
            var msg = new StatusMessage
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.Now,
                IsIndeterminate = false
            };

            // record last outcome for Clear behavior on success/error
            if (type == StatusMessageType.Success || type == StatusMessageType.Error)
            {
                _lastOutcomeMessage = message;
                _lastOutcomeType = type;
            }

            PushMessage(msg);
        }

        public void ShowProgress(string message, double progress, bool isIndeterminate = false)
        {
            // Normalize progress: if caller supplies 0..1 interpret as fraction and convert to percentage (0..100).
            double normalized = progress;
            if (!double.IsNaN(progress) && progress >= 0.0 && progress <= 1.0)
            {
                normalized = progress * 100.0;
            }

            var msg = new StatusMessage
            {
                Message = message,
                Type = StatusMessageType.Progress,
                Progress = normalized,
                IsIndeterminate = isIndeterminate,
                Timestamp = DateTime.Now
            };

            PushMessage(msg);
        }

        public void Clear()
        {
            // Instead of removing the last message, push an outcome message (keeps statusbar content stable).
            var outcomeText = !string.IsNullOrEmpty(_lastOutcomeMessage) ? _lastOutcomeMessage : "Ready";
            var outcomeMsg = new StatusMessage
            {
                Message = outcomeText,
                Type = _lastOutcomeType,
                Timestamp = DateTime.Now,
                IsIndeterminate = false
            };
            PushMessage(outcomeMsg);
        }

        public void ShowError(string message, Exception exception = null)
        {
            var msg = new StatusMessage
            {
                Message = message,
                Type = StatusMessageType.Error,
                Exception = exception,
                Timestamp = DateTime.Now
            };

            _lastOutcomeMessage = message;
            _lastOutcomeType = StatusMessageType.Error;

            PushMessage(msg);
        }

        public void ShowWarning(string message)
        {
            var msg = new StatusMessage
            {
                Message = message,
                Type = StatusMessageType.Warning,
                Timestamp = DateTime.Now
            };

            PushMessage(msg);
        }

        public void ShowSuccess(string message)
        {
            var msg = new StatusMessage
            {
                Message = message,
                Type = StatusMessageType.Success,
                Timestamp = DateTime.Now
            };

            _lastOutcomeMessage = message;
            _lastOutcomeType = StatusMessageType.Success;

            PushMessage(msg);
        }

        public void SetProjectPath(string path)
        {
            ProjectPath = path ?? string.Empty;
        }

        public void SetJsonFolder(string folder)
        {
            JsonFolder = folder ?? string.Empty;
        }

        private void PushMessage(StatusMessage msg)
        {
            _history.Add(msg);
            CurrentMessage = msg;
        }

        private void ShowHistory()
        {
            if (_history.Count > 0)
            {
                CurrentMessage = _history[_history.Count - 1];
            }
        }

        private void RaiseAllMessagePropertiesChanged(StatusMessage previous)
        {
            OnPropertyChanged(nameof(CurrentMessage));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(MessageType));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(IsIndeterminate));
            OnPropertyChanged(nameof(IsVisible));
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
