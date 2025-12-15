using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DarkSunRE.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }        

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(T parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        bool ICommand.CanExecute(object parameter)
        {
            // Обработка специальных случаев
            if (parameter == null && default(T) == null)
                return CanExecute(default(T));

            if (parameter is T typedParameter)
                return CanExecute(typedParameter);

            // Попытка преобразования
            try
            {
                var converted = (T)Convert.ChangeType(parameter, typeof(T));
                return CanExecute(converted);
            }
            catch
            {
                return false;
            }
        }

        public void Execute(T parameter)
        {
            _execute(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            if (parameter is T typedParameter)
            {
                Execute(typedParameter);
            }
            else if (parameter == null && default(T) == null)
            {
                Execute(default(T));
            }
            else
            {
                // Попытка преобразования типа
                try
                {
                    var converted = (T)Convert.ChangeType(parameter, typeof(T));
                    Execute(converted);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot convert parameter of type {parameter?.GetType().Name} to {typeof(T).Name}", ex);
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
