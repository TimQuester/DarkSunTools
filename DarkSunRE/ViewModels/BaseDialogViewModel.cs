using DarkSunRE.Commands;
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
    public abstract partial class BaseDialogViewModel : INotifyPropertyChanged
    {
        public delegate void ClosedHandler(object sender, EventArgs eventArgs);
        public event EventHandler<DialogResultEventArgs> RequestClose;

        public string Title { get; set; }
        public bool? DialogResult { get; set; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected BaseDialogViewModel()
        {
            OkCommand = new RelayCommand(Ok, CanOk);
            CancelCommand = new RelayCommand(Cancel);
        }

        protected virtual void Ok() => DialogResult = true;
        protected virtual void Cancel() => DialogResult = false;
        protected virtual bool CanOk() => true;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        
        public event EventHandler<DialogResultEventArgs> DialogClosed;

        protected virtual void OnOk(object resultData = null)
        {
            CloseDialog(true, resultData);
        }
        protected virtual void OnCancel()
        {
            CloseDialog(false, null);
        }
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected virtual void CloseDialog(bool? dialogResult = null, object resultData = null)
        {
            DialogResult = dialogResult;
            RequestClose?.Invoke(this, new DialogResultEventArgs(dialogResult, resultData));
        }
    }
}
