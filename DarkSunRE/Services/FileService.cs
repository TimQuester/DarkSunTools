using DarkSunRE.Models;
using Microsoft.Win32;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DarkSunRE.Services
{
    public class FileService : IFileService
    {
        private readonly IGameDefinitionService _gameDefinitionService;
        
        private readonly Window _owner;

        public FileService(IGameDefinitionService gameDefinitionService)
        {
            _gameDefinitionService = gameDefinitionService;
            _owner = Application.Current.MainWindow;
        }

        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void DeleteFile(string path) => File.Delete(path);
        public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(path, recursive);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
        public string ReadAllText(string path, Encoding encoding = null) =>
            File.ReadAllText(path, encoding ?? Encoding.Default);

        public void WriteAllBytes(string path, byte[] data) => File.WriteAllBytes(path, data);
        public void WriteAllText(string path, string content, Encoding encoding = null) =>
            File.WriteAllText(path, content, encoding ?? Encoding.Default);

        public string[] GetFiles(string path, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly) =>
            Directory.GetFiles(path, searchPattern, option);

        public string[] GetDirectories(string path, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly) =>
            Directory.GetDirectories(path, searchPattern, option);

        public long GetFileSize(string path) => new FileInfo(path).Length;
        public DateTime GetFileLastWriteTime(string path) => File.GetLastWriteTime(path);
        public DateTime GetFileCreationTime(string path) => File.GetCreationTime(path);

        public string ShowOpenFileDialog(string filter = "All files|*.*", string initialDirectory = null, string title = "Open File")
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = title
            };

            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
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

            return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
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

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string ShowFolderDialog(string description = "Select folder", string initialDirectory = null)
        {
            var dialog = new OpenFolderDialog
            {
                Title = description,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            return dialog.ShowDialog(_owner) == true ? dialog.FolderName : null;
        }

        public string CombinePaths(params string[] paths) => Path.Combine(paths);
        public string GetFileName(string path) => Path.GetFileName(path);
        public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path);
        public string GetExtension(string path) => Path.GetExtension(path);
        public string ChangeExtension(string path, string extension) => Path.ChangeExtension(path, extension);

        public string GetTempFileName(string extension = ".tmp")
        {
            var tempPath = Path.GetTempFileName();
            if (!string.IsNullOrEmpty(extension))
            {
                var newPath = Path.ChangeExtension(tempPath, extension);
                File.Move(tempPath, newPath);
                return newPath;
            }
            return tempPath;
        }

        public string GetTempDirectory()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        public bool IsValidGamePath(string path, ProjectType gameType)
        {
            if (!DirectoryExists(path))
                return false;

            var expectedFiles = _gameDefinitionService.GetExpectedFiles(gameType);
            return expectedFiles.All(file => FileExists(Path.Combine(path, file)));
        }

        public string[] GetExpectedGameFiles(ProjectType gameType) =>
            _gameDefinitionService.GetExpectedFiles(gameType);

        public string DetectGameVersion(string gamePath, ProjectType gameType)
        {
            // Логика определения версии игры
            // Например, по размеру или содержимому EXE-файла
            return "1.0";
        }

        // Асинхронные методы
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var buffer = new byte[fileStream.Length];
            await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            return buffer;
        }

        public async Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(path, encoding ?? Encoding.Default);
            return await reader.ReadToEndAsync();
        }

        public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
        }

        public async Task WriteAllTextAsync(string path, string content, Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            using var writer = new StreamWriter(path, false, encoding ?? Encoding.Default);
            await writer.WriteAsync(content);
        }
    }
}
