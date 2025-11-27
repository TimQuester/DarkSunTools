using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class StatusService : IStatusService
    {
        private readonly ObservableCollection<StatusMessage> _messageHistory = new();
        private StatusMessage _currentMessage;

        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        public IReadOnlyCollection<StatusMessage> MessageHistory => _messageHistory;

        public StatusMessage CurrentMessage
        {
            get => _currentMessage;
            private set
            {
                var previous = _currentMessage;
                _currentMessage = value;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(value, previous));
            }
        }

        public void ShowMessage(string message, StatusMessageType type = StatusMessageType.Info, TimeSpan? timeout = null)
        {
            var statusMessage = new StatusMessage
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            };

            AddToHistory(statusMessage);
            CurrentMessage = statusMessage;

            if (timeout.HasValue)
            {
                Task.Delay(timeout.Value).ContinueWith(_ => Clear(), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public void ShowProgress(string message, double progress, bool isIndeterminate = false)
        {
            var statusMessage = new StatusMessage
            {
                Message = message,
                Type = StatusMessageType.Progress,
                Progress = progress,
                IsIndeterminate = isIndeterminate,
                Timestamp = DateTime.Now
            };

            CurrentMessage = statusMessage;
        }

        public void Clear() => CurrentMessage = null;

        public void ShowError(string message, Exception exception = null)
        {
            ShowMessage(message, StatusMessageType.Error);
            // Можно логировать exception
        }

        public void ShowWarning(string message) => ShowMessage(message, StatusMessageType.Warning);
        public void ShowSuccess(string message) => ShowMessage(message, StatusMessageType.Success);

        private void AddToHistory(StatusMessage message)
        {
            _messageHistory.Add(message);
            // Ограничиваем историю, например, 100 сообщений
            if (_messageHistory.Count > 100)
                _messageHistory.RemoveAt(0);
        }
    }
}
