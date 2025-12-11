using DarkSunRE.Models;
using DSunTools.Common.Gff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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

        // Synchronous compatibility wrapper - delegates to async implementation
        public Project CreateNewProject(string originalPath, string modifiedPath, string gitPath, ProjectType gameType)
        {
            return CreateNewProjectAsync(originalPath, modifiedPath, gitPath, gameType, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Async implementation of project creation (copies files and loads resources)
        public async Task<Project> CreateNewProjectAsync(object originalGamePathObj, object modifiedGamePathObj, object JsonFolderPathObj, object type)
        {
            // Interface uses object parameters; coerce to expected types
            var originalPath = originalGamePathObj as string ?? throw new ArgumentException("originalGamePath must be a string", nameof(originalGamePathObj));
            var modifiedPath = modifiedGamePathObj as string ?? throw new ArgumentException("modifiedGamePath must be a string", nameof(modifiedGamePathObj));
            var jsonPath = JsonFolderPathObj as string;
            var gameType = type is ProjectType pt ? pt : throw new ArgumentException("type must be ProjectType", nameof(type));

            return await CreateNewProjectAsync(originalPath, modifiedPath, jsonPath, gameType, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<Project> CreateNewProjectAsync(string originalPath, string modifiedPath, string jsonPath, ProjectType gameType, CancellationToken cancellationToken)
        {
            if (!_fileService.IsValidGamePath(originalPath, gameType))
                throw new InvalidOperationException("Invalid game path");

            if (!_fileService.DirectoryExists(modifiedPath))
                _fileService.CreateDirectory(modifiedPath);

            if (jsonPath != null && !_fileService.DirectoryExists(jsonPath))
                _fileService.CreateDirectory(jsonPath);

            var expectedFiles = _fileService.GetExpectedGameFiles(gameType);

            // Copy files asynchronously
            //foreach (var file in expectedFiles)
            //{
            //    cancellationToken.ThrowIfCancellationRequested();

            //    var sourcePath = _fileService.CombinePaths(originalPath, file);
            //    var destPath = _fileService.CombinePaths(modifiedPath, file);

            //    if (_fileService.FileExists(sourcePath))
            //    {
            //        //TODO: freezes
            //        var data = await _fileService.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            //        await _fileService.WriteAllBytesAsync(destPath, data, cancellationToken).ConfigureAwait(false);
            //    }
            //}

            var project = new Project
            {
                OriginalGamePath = originalPath,
                ModifiedGamePath = modifiedPath,
                JsonFolderPath = jsonPath,
                ProjectType = gameType,
            };

            // Load resource files asynchronously (CPU-bound parsing pushed to thread pool)
            project.Files = await LoadResourceFilesAsync(project.ProjectType, project.OriginalGamePath, null, cancellationToken).ConfigureAwait(false);

            OnProjectChanged(ProjectAction.Created, project);
            return project;
        }

        private List<ResourceFile> LoadResourceFiles(ProjectType projectType, string originalGamePath)
        {
            // keep sync wrapper for compatibility
            return LoadResourceFilesAsync(projectType, originalGamePath, null, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Async loader with progress and cancellation
        public async Task<List<ResourceFile>> LoadResourceFilesAsync(ProjectType projectType, string originalGamePath, IProgress<StatusMessage> progress = null, CancellationToken cancellationToken = default)
        {
            switch (projectType)
            {
                case ProjectType.DarkSunShatteredLands:
                    return await LoadDSSLFilesAsync(originalGamePath, progress, cancellationToken).ConfigureAwait(false);
                default:
                    return new List<ResourceFile>();
            }
        }

        // Async GFF loader
        public async Task<List<ResourceFile>> LoadDSSLFilesAsync(string originalGamePath, IProgress<StatusMessage> progress = null, CancellationToken cancellationToken = default)
        {
            var resourceList = new List<ResourceFile>();

            if (string.IsNullOrWhiteSpace(originalGamePath) || !_fileService.DirectoryExists(originalGamePath))
                return resourceList;

            var files = _fileService.GetFiles(originalGamePath, "*.gff", SearchOption.TopDirectoryOnly);
            int total = files.Length;
            int processed = 0;

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                var fileName = Path.GetFileName(filePath);

                progress?.Report(new StatusMessage
                {
                    Message = $"Loading {fileName}",
                    Type = StatusMessageType.Progress,
                    Progress = (double)(processed - 1) / Math.Max(1, total),
                    IsIndeterminate = false,
                    Timestamp = DateTime.Now
                });

                try
                {
                    var bytes = await _fileService.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

                    if (bytes == null || bytes.Length < 16)
                    {
                        _statusService?.ShowWarning($"Skipping {fileName}: file too small.");
                        continue;
                    }

                    // quick sanity-check of index pointer at offset 12
                    bool indexOk;
                    try
                    {
                        int indexStart = BitConverter.ToInt32(bytes, 12);
                        indexOk = indexStart >= 0 && indexStart < bytes.Length;
                    }
                    catch
                    {
                        indexOk = false;
                    }

                    if (!indexOk)
                    {
                        var headerHex = BitConverter.ToString(bytes.Take(Math.Min(32, bytes.Length)).ToArray());
                        _statusService?.ShowWarning($"Skipping {fileName}: invalid index start. Size={bytes.Length}. Header={headerHex}");
                        continue;
                    }

                    // Parse GFF on threadpool (CPU-bound)
                    GffFile gffFile;
                    try
                    {
                        gffFile = await Task.Run(() => GffFile.Create(bytes), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var headerHex = BitConverter.ToString(bytes.Take(Math.Min(64, bytes.Length)).ToArray());
                        _statusService?.ShowWarning($"Failed to parse GFF '{fileName}': {ex.GetType().Name}: {ex.Message}. Size={bytes.Length}. Header={headerHex}");
                        continue;
                    }

                    var resourceFile = new ResourceFile
                    {
                        Name = fileName,
                        FullPath = filePath,
                        Data = bytes,
                        IsModified = false,
                        Resources = ExtractResources(gffFile)
                    };

                    resourceList.Add(resourceFile);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _statusService?.ShowWarning($"Ошибка при загрузке GFF: {filePath} - {ex.Message}");
                    continue;
                }
            }

            progress?.Report(new StatusMessage
            {
                Message = $"Loaded {resourceList.Count} resource files",
                Type = StatusMessageType.Success,
                Progress = 1.0,
                IsIndeterminate = false,
                Timestamp = DateTime.Now
            });

            return resourceList;
        }

        // Synchronous LoadProject wrapper delegates to async
        public Project LoadProject(string projectFilePath)
        {
            return LoadProjectAsync(projectFilePath, null, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Async load project with progress and cancellation
        public async Task<Project> LoadProjectAsync(string projectFilePath, IProgress<StatusMessage> progress = null, CancellationToken cancellationToken = default)
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
                progress?.Report(new StatusMessage { Message = "Reading project file...", Type = StatusMessageType.Progress, IsIndeterminate = true, Timestamp = DateTime.Now });

                var json = await _fileService.ReadAllTextAsync(projectFilePath, null, cancellationToken).ConfigureAwait(false);
                var dto = JsonSerializer.Deserialize<ProjectFileDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                    JsonFolderPath = dto.JsonFolderPath,
                    Created = dto.CreatedDate ?? DateTime.Now,
                    LastModified = dto.LastModifiedDate ?? File.GetLastWriteTime(projectFilePath),
                    ProjectType = dto.GameType,
                    TransliterationRules = dto.TransliterationRules ?? new List<TransliterationRule>()
                };

                progress?.Report(new StatusMessage { Message = "Loading resource files...", Type = StatusMessageType.Progress, IsIndeterminate = false, Progress = 0, Timestamp = DateTime.Now });

                project.Files = await LoadResourceFilesAsync(project.ProjectType, project.OriginalGamePath ?? project.ModifiedGamePath, progress, cancellationToken).ConfigureAwait(false);

                OnProjectChanged(ProjectAction.Opened, project);
                return project;
            }
            catch (OperationCanceledException)
            {
                _statusService?.ShowMessage("Project load cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to load project: {ex.Message}", ex);
                return null;
            }
        }

        // Synchronous SaveProject wrapper
        public void SaveProject(Project project)
        {
            SaveProjectAsync(project, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Async save project
        public async Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(project.ModifiedGamePath) || string.IsNullOrWhiteSpace(project.ProjectName))
            {
                _statusService?.ShowError("Project must have a name and a modified game path to be saved.");
                return;
            }

            try
            {
                _statusService?.ShowProgress("Saving project...", 0, true);

                var dto = new ProjectFileDto
                {
                    ProjectName = project.ProjectName,
                    OriginalGamePath = project.OriginalGamePath,
                    ModifiedGamePath = project.ModifiedGamePath,
                    JsonFolderPath = project.JsonFolderPath,
                    GameType = project.ProjectType,
                    CreatedDate = project.Created,
                    LastModifiedDate = DateTime.Now,
                    TransliterationRules = project.TransliterationRules
                };

                if (!_fileService.DirectoryExists(project.ModifiedGamePath))
                    _fileService.CreateDirectory(project.ModifiedGamePath);

                var projectFilePath = Path.Combine(project.ModifiedGamePath, $"{MakeValidFileName(project.ProjectName)}.dsproj");
                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });

                await _fileService.WriteAllTextAsync(projectFilePath, json, null, cancellationToken).ConfigureAwait(false);

                project.LastModified = DateTime.Now;
                OnProjectChanged(ProjectAction.Saved, project);
                _statusService?.ShowSuccess($"Project saved: {projectFilePath}");
            }
            catch (OperationCanceledException)
            {
                _statusService?.ShowMessage("Save cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to save project: {ex.Message}", ex);
            }
            finally
            {
                _statusService?.Clear();
            }
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

        public Task SaveProjectAsync(Project project)
        {
            return SaveProjectAsync(project, CancellationToken.None);
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

        public Task ScanGameResourcesAsync(Project project)
        {
            throw new NotImplementedException();
        }

        private class ProjectFileDto
        {
            public string ProjectName { get; set; }
            public string OriginalGamePath { get; set; }
            public string ModifiedGamePath { get; set; }
            public string JsonFolderPath { get; set; }
            public ProjectType GameType { get; set; }
            public DateTime? CreatedDate { get; set; }
            public DateTime? LastModifiedDate { get; set; }
            public List<TransliterationRule> TransliterationRules { get; set; }
        }
    }
}
