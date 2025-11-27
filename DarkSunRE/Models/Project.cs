using System.IO;

namespace DarkSunRE.Models
{
    public class Project
    {
        private string path;

        public Project()
        {

        }

        public string ProjectName { get; set; }

        public Project(string fileName)
        {
            Path = fileName;
        }

        public string Path
        {
            get => path;
            set
            {
                Files.Clear();
                Files = FillFiles(value);
                path = value;
            }
        }

        public List<ResourceFile> Files { get; set; } = new List<ResourceFile>();

        public void LoadFileData(ResourceFile file)
        {

            file.Data = File.ReadAllBytes(file.FullPath);
        }

        List<ResourceFile> FillFiles(string path)
        {
            string searchPattern = "GPL*.*";
            List<ResourceFile> resourceFiles = new List<ResourceFile>();
            List<string> filePaths = Directory.GetFiles(path, searchPattern).ToList<string>();
            foreach (string filePath in filePaths)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                var file = new GPLFileType();
                file.FullPath = filePath;
                file.Name = fileInfo.Name;
                resourceFiles.Add(file);
            }
            return resourceFiles;
        }

        public List<GPLDocument> OpenedDocuments = new List<GPLDocument>();

        #region новая структура, выделяем модель
        /// <summary>
        /// Путь к оригинальной игре
        /// </summary>
        public string OriginalGamePath { get; set; }
        /// <summary>
        /// Путь где будут собираться изменённые файлы
        /// </summary>
        public string ModifiedGamePath { get; set; }
        /// <summary>
        /// Путь к репозиторию с сериализованными данными
        /// </summary>
        public string GitRepositoryPath { get; set; }                
        /// <summary>
        /// Дата создания проекта
        /// </summary>
        public DateTime Created { get; set; }
        /// <summary>
        /// Дата последней модификации проекта
        /// </summary>
        public DateTime LastModified { get; set; }
        public ProjectType ProjectType { get; set; }
        #endregion
    }
}
