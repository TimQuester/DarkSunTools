using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DarkSunRE.Services
{
    public class UiService : IUiService
    {
        public string ShowOpenFileDialog(string filter = "All files|*.*", string initialDirectory = null, string title = "Open File")
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = title
            };

            var owner = Application.Current?.MainWindow;
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        public string[] ShowOpenMultipleFilesDialog(string filter = "All files|*.*", string initialDirectory = null, string title = "Open Files")
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = title,
                Multiselect = true
            };

            var owner = Application.Current?.MainWindow;
            return dialog.ShowDialog(owner) == true ? dialog.FileNames : Array.Empty<string>();
        }

        public string ShowSaveFileDialog(string defaultFileName = null, string filter = "All files|*.*", string initialDirectory = null, string title = "Save File")
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = title,
                FileName = defaultFileName
            };

            var owner = Application.Current?.MainWindow;
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        public string ShowFolderDialog(string description = "Select folder", string initialDirectory = null)
        {
            var dialog = new OpenFolderDialog
            {
                Title = description,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            var owner = Application.Current?.MainWindow;
            return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
        }

        public Task<string> ShowOpenFileDialogAsync(string filter = "All files|*.*", string initialDirectory = null, string title = "Open File")
        {
            // В текущем WPF ShowDialog синхронен, при необходимости оборачиваем вызов в Task.Run или возвращаем синхронный результат.
            return Task.FromResult(ShowOpenFileDialog(filter, initialDirectory, title));
        }
    }
}