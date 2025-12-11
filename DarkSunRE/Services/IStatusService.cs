using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public interface IStatusService
    {
        void ShowMessage(string message, StatusMessageType type = StatusMessageType.Info, TimeSpan? timeout = null);
        void ShowProgress(string message, double progress, bool isIndeterminate = false);
        void Clear();
        void ShowError(string message, Exception exception = null);
        void ShowWarning(string message);
        void ShowSuccess(string message);

        IReadOnlyCollection<StatusMessage> MessageHistory { get; }
        StatusMessage CurrentMessage { get; }
        event EventHandler<StatusChangedEventArgs> StatusChanged;
        public void SetProjectPath(string path);
        public void SetJsonFolder(string folder);        
    }

    public enum StatusMessageType
    {
        Info,
        Warning,
        Error,
        Success,
        Progress
    }

    public class StatusMessage
    {
        public string Message { get; set; }
        public StatusMessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Progress { get; set; }
        public bool IsIndeterminate { get; set; }
        public Exception Exception { get; set; }
    }

    public class StatusChangedEventArgs : EventArgs
    {
        private StatusMessage value;
        private StatusMessage previous;

        public StatusChangedEventArgs(StatusMessage value, StatusMessage previous)
        {
            this.value = value;
            this.previous = previous;
        }

        public StatusMessage NewMessage { get; }
        public StatusMessage PreviousMessage { get; }
    }
}
