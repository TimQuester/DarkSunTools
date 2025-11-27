using DarkSunRE.Models;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IFileService _fileService;
        private readonly IStatusService _statusService;
        private readonly IResourceService _resourceService;
    
        public ProjectService(IFileService fileService, IStatusService statusService, IResourceService resourceService)
        {
            _fileService = fileService;
            _statusService = statusService;
            _resourceService = resourceService;
        }
        #region Events
        public event EventHandler<ProjectEventArgs> ProjectChanged;
        public event EventHandler<ResourceEventArgs> ResourceChanged;
        public event EventHandler<GffFileEventArgs> GffFileChanged;
        public event EventHandler<ProjectValidationEventArgs> ProjectValidated;
        public event EventHandler<GitOperationEventArgs> GitOperationPerformed;
        #endregion

        protected virtual void OnProjectChanged(ProjectAction action, Project project)
        {
            ProjectChanged?.Invoke(this, new ProjectEventArgs(action, project));
        }

        public Project CreateNewProject(string originalPath, string modifiedPath, string gitPath, ProjectType gameType)
        {
            if (!_fileService.IsValidGamePath(originalPath, gameType))
                throw new InvalidOperationException("Invalid game path");
                    
            if (!_fileService.DirectoryExists(modifiedPath))
                    _fileService.CreateDirectory(modifiedPath);
        
            if (gitPath != null && !_fileService.DirectoryExists(gitPath))
                _fileService.CreateDirectory(gitPath);
        
            // Копируем необходимые файлы
            var expectedFiles = _fileService.GetExpectedGameFiles(gameType);
            foreach (var file in expectedFiles)
            {
                var sourcePath = _fileService.CombinePaths(originalPath, file);
                var destPath = _fileService.CombinePaths(modifiedPath, file);
            
                if (_fileService.FileExists(sourcePath))
                {
                    var data = _fileService.ReadAllBytes(sourcePath);
                    _fileService.WriteAllBytes(destPath, data);
                }
            }
        
            var project = new Project
            {
                OriginalGamePath = originalPath,
                ModifiedGamePath = modifiedPath,
                GitRepositoryPath = gitPath,
                ProjectType = gameType,
                //Version = _fileService.DetectGameVersion(originalPath, gameType)
            };

            project.Files = LoadResourceFiles(project.ProjectType, project.OriginalGamePath);

            return project;
        }

        private List<ResourceFile> LoadResourceFiles(ProjectType projectType, string OriginalGamePath)
        {
            switch (projectType)
            {
                case ProjectType.DarkSunShatteredLands: return LoadDSSLFiles(OriginalGamePath);
                default: return null;
            }
        }

        private List<ResourceFile> LoadDSSLFiles(string OriginalGamePath)
        {
            var resourceList = new List<ResourceFile>();

            if (!Directory.Exists(OriginalGamePath))
                return null;
            
            var searchPatterns = new[] { "*.gff"};

            foreach (var pattern in searchPatterns)
            {
                var files = Directory.GetFiles(OriginalGamePath, pattern, SearchOption.TopDirectoryOnly);

                foreach (var filePath in files)
                {
                    try
                    {                        
                        var resourceFile =
                        new ResourceFile
                        {
                            Name = Path.GetFileName(filePath),
                            FullPath = filePath,
                            Data = File.ReadAllBytes(filePath),
                            IsModified = false                            
                        };
                        GffFile gffFile = GffFile.Create(resourceFile.Data);
                        resourceFile.Resources = ExtractResources(gffFile);
                        resourceList.Add(resourceFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка: {filePath} - {ex.Message}");
                        continue;
                    }
                }
            }

            return resourceList;

        }

        public Project CreateNewProject(string originalPath, string modifiedPath, string gitPath)
        {
            var project = new Project();
            OnProjectChanged(ProjectAction.Created, project);

            return project;
        }

        public Project LoadProject(string projectFilePath)
        {
            throw new NotImplementedException();
        }

        public void SaveProject(Project project)
        {
            OnProjectChanged(ProjectAction.Saved, project);
        }

        public void ScanGameResources(Project project)
        {
            throw new NotImplementedException();
        }

        public void ReplaceResource(Project project, string gffFileName, ResourceInfo resource, byte[] newData)
        {
            OnResourceChanged(ProjectAction.ResourceReplaced, project, resource, gffFileName);
            OnProjectChanged(ProjectAction.Modified, project);
        }

        protected virtual void OnResourceChanged(ProjectAction action, Project project, ResourceInfo resource, string gffFileName)
        {
            ResourceChanged?.Invoke(this, new ResourceEventArgs(action, project, resource, gffFileName));
        }

        protected virtual void OnGffFileChanged(ProjectAction action, Project project, GffFileInfo gffFile, string[] modifiedResources = null)
        {
            GffFileChanged?.Invoke(this, new GffFileEventArgs(action, project, gffFile, modifiedResources));
        }

        public void ScanGameResourcesAsync(Project project)
        {
            throw new NotImplementedException();
        }


        public Task<Project> CreateNewProjectAsync(object originalGamePath, object modifiedGamePath, object gitRepositoryPath, object type)
        {
            throw new NotImplementedException();
        }

        Task IProjectService.ScanGameResourcesAsync(Project project)
        {
            throw new NotImplementedException();
        }

        public Task SaveProjectAsync(Project project)
        {
            throw new NotImplementedException();
        }

        public List<ResourceInfo> ExtractResources(GffFile gffFile)
        {
            var resources = new List<ResourceInfo>();
            var descriptors = gffFile.DescribeResources();

            foreach (var descriptor in descriptors)
            {
                var resourceData = gffFile.GetResourceData(descriptor.Tag, descriptor.Number);
                var resourceType = _resourceService.DetectResourceType(descriptor.Tag, resourceData);

                resources.Add(new ResourceInfo
                {
                    Tag = descriptor.Tag,
                    Number = descriptor.Number,
                    Offset = descriptor.Offset,
                    Size = descriptor.Size,
                    Type = resourceType,
                    //Description = _resourceService.GetResourceDescription(descriptor.Tag, descriptor.Number)
                    Description = "test"
                });
            }

            return resources;
        }

    }
}
