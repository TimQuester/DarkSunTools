using DarkSunRE.Commands;
using DarkSunRE.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        private readonly IStatusService _statusService;
        private string _message;
        private StatusMessageType _messageType;
        private double _progress;
        private bool _isIndeterminate;
        private bool _isVisible;

        public StatusBarViewModel(IStatusService statusService)
        {
            _statusService = statusService;
            _statusService.StatusChanged += OnStatusChanged;

            ClearCommand = new RelayCommand(Clear);
            //ShowHistoryCommand = new RelayCommand(ShowHistory);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public StatusMessageType MessageType
        {
            get => _messageType;
            set => SetProperty(ref _messageType, value);
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public ICommand ClearCommand { get; }
        public ICommand ShowHistoryCommand { get; }

        private void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (e.NewMessage == null)
            {
                IsVisible = false;
                Message = string.Empty;
                return;
            }

            Message = e.NewMessage.Message;
            MessageType = e.NewMessage.Type;
            Progress = e.NewMessage.Progress ?? 0;
            IsIndeterminate = e.NewMessage.IsIndeterminate;
            IsVisible = true;

            // Автоматическое скрытие информационных сообщений
            if (e.NewMessage.Type == StatusMessageType.Info)
            {
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    if (_statusService.CurrentMessage == e.NewMessage)
                        Clear();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void Clear() => _statusService.Clear();
        //private void ShowHistory() => /* Показать окно с историей */;

        // INotifyPropertyChanged implementation
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
