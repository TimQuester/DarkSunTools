using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    /// <summary>
    /// UI-related dialogs and visual helpers Ч отделены от IFileService дл€ тестируемости и разделени€ об€занностей.
    /// </summary>
    public interface IUiService
    {
        string ShowOpenFileDialog(string filter = "All files|*.*", string initialDirectory = null, string title = "Open File");
        string[] ShowOpenMultipleFilesDialog(string filter = "All files|*.*", string initialDirectory = null, string title = "Open Files");
        string ShowSaveFileDialog(string defaultFileName = null, string filter = "All files|*.*", string initialDirectory = null, string title = "Save File");
        string ShowFolderDialog(string description = "Select folder", string initialDirectory = null);

        // Asynchronous wrapper (optional convenience)
        Task<string> ShowOpenFileDialogAsync(string filter = "All files|*.*", string initialDirectory = null, string title = "Open File");
    }
}