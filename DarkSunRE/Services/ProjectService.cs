using DarkSunRE.Models;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IFileService _fileService;
        private readonly IStatusService _statusService;
        private readonly IResourceService _resource_service;

        public ProjectService(IFileService fileService, IStatusService statusService, IResourceService resourceService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
            _resource_service = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
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
            };

            project.Files = LoadResourceFiles(project.ProjectType, project.OriginalGamePath);

            return project;
        }

        private List<ResourceFile> LoadResourceFiles(ProjectType projectType, string originalGamePath)
        {
            switch (projectType)
            {
                case ProjectType.DarkSunShatteredLands: return LoadDSSLFiles(originalGamePath);
                default: return new List<ResourceFile>();
            }
        }

        private List<ResourceFile> LoadDSSLFiles(string originalGamePath)
        {
            var resourceList = new List<ResourceFile>();

            if (string.IsNullOrWhiteSpace(originalGamePath) || !_fileService.DirectoryExists(originalGamePath))
                return resourceList;

            var files = _fileService.GetFiles(originalGamePath, "*.gff", SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                try
                {
                    var resourceFile = new ResourceFile
                    {
                        Name = Path.GetFileName(filePath),
                        FullPath = filePath,
                        Data = _fileService.ReadAllBytes(filePath),
                        IsModified = false
                    };

                    // парсинг GFF (синхронно, как раньше)
                    GffFile gffFile = GffFile.Create(resourceFile.Data);
                    resourceFile.Resources = ExtractResources(gffFile);
                    resourceList.Add(resourceFile);
                }
                catch (Exception ex)
                {
                    // не останавливаем загрузку — логируем и продолжаем
                    _statusService?.ShowWarning($"Ошибка при загрузке GFF: {filePath} - {ex.Message}");
                    continue;
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
            if (string.IsNullOrWhiteSpace(projectFilePath))
                throw new ArgumentNullException(nameof(projectFilePath));

            if (!_fileService.FileExists(projectFilePath))
            {
                _statusService?.ShowError($"Project file not found: {projectFilePath}");
                return null;
            }

            try
            {
                var json = _fileService.ReadAllText(projectFilePath);
                var dto = JsonSerializer.Deserialize<ProjectFileDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (dto == null)
                {
                    _statusService?.ShowError("Project file is empty or invalid.");
                    return null;
                }

                var projectFolder = dto.ModifiedGamePath ?? dto.OriginalGamePath ?? string.Empty;
                var project = new Project(projectFolder)
                {
                    ProjectName = dto.ProjectName,
                    OriginalGamePath = dto.OriginalGamePath,
                    ModifiedGamePath = dto.ModifiedGamePath,
                    GitRepositoryPath = dto.GitRepositoryPath,
                    Created = dto.CreatedDate ?? DateTime.Now,
                    LastModified = dto.LastModifiedDate ?? File.GetLastWriteTime(projectFilePath),
                    ProjectType = dto.GameType
                };

                // синхронная загрузка файлов (как раньше)
                project.Files = LoadResourceFiles(project.ProjectType, project.OriginalGamePath ?? project.ModifiedGamePath);

                OnProjectChanged(ProjectAction.Opened, project);
                return project;
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to load project: {ex.Message}", ex);
                return null;
            }
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
            // backward compatibility - call sync SaveProject
            SaveProject(project);
            return Task.CompletedTask;
        }

        public List<ResourceInfo> ExtractResources(GffFile gffFile)
        {
            var resources = new List<ResourceInfo>();
            var descriptors = gffFile.DescribeResources();

            foreach (var descriptor in descriptors)
            {
                var resourceData = gffFile.GetResourceData(descriptor.Tag, descriptor.Number);
                var resourceType = _resource_service.DetectResourceType(descriptor.Tag, resourceData);

                resources.Add(new ResourceInfo
                {
                    Tag = descriptor.Tag,
                    Number = descriptor.Number,
                    Offset = descriptor.Offset,
                    Size = descriptor.Size,
                    Type = resourceType,
                    Description = "test"
                });
            }

            return resources;
        }

        private string MakeValidFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(ch => !invalidChars.Contains(ch)).ToArray());
        }

        private class ProjectFileDto
        {
            public string ProjectName { get; set; }
            public string OriginalGamePath { get; set; }
            public string ModifiedGamePath { get; set; }
            public string GitRepositoryPath { get; set; }
            public ProjectType GameType { get; set; }
            public DateTime? CreatedDate { get; set; }
            public DateTime? LastModifiedDate { get; set; }
        }
    }
}
