using DarkSunRE.Models;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public interface IFileService
    {
        // Файловые операции
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteFile(string path);
        void DeleteDirectory(string path, bool recursive = false);

        // Чтение/запись
        byte[] ReadAllBytes(string path);
        string ReadAllText(string path, Encoding encoding = null);
        void WriteAllBytes(string path, byte[] data);
        void WriteAllText(string path, string content, Encoding encoding = null);

        // Поиск файлов
        string[] GetFiles(string path, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly);
        string[] GetDirectories(string path, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly);

        // Информация о файлах
        long GetFileSize(string path);
        DateTime GetFileLastWriteTime(string path);
        DateTime GetFileCreationTime(string path);

        // Пути
        string CombinePaths(params string[] paths);
        string GetFileName(string path);
        string GetFileNameWithoutExtension(string path);
        string GetDirectoryName(string path);
        string GetExtension(string path);
        string ChangeExtension(string path, string extension);

        // Временные файлы
        string GetTempFileName(string extension = ".tmp");
        string GetTempDirectory();

        // Специфичные для приложения
        bool IsValidGamePath(string path, ProjectType gameType);
        string[] GetExpectedGameFiles(ProjectType gameType);
        string DetectGameVersion(string gamePath, ProjectType gameType);

        // Асинхронные версии
        Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
        Task<string> ReadAllTextAsync(string path, Encoding encoding = null, CancellationToken cancellationToken = default);
        Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);
        Task WriteAllTextAsync(string path, string content, Encoding encoding = null, CancellationToken cancellationToken = default);
    }
}