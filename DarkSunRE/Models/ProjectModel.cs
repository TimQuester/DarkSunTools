using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualBasic;

namespace DarkSunRE.Models
{
    class ProjectModel
    {
        private string path;

        public ProjectModel()
        {

        }

        public ProjectModel(string fileName)
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

        public List<GPLDocumentModel> OpenedDocuments = new List<GPLDocumentModel>();
    }
}
