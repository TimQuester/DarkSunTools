using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public interface IProjectService
    {
        #region Events
        event EventHandler<ProjectEventArgs> ProjectChanged;
        event EventHandler<ResourceEventArgs> ResourceChanged;
        event EventHandler<GffFileEventArgs> GffFileChanged;
        event EventHandler<ProjectValidationEventArgs> ProjectValidated;
        event EventHandler<GitOperationEventArgs> GitOperationPerformed;
        #endregion
        public Project CreateNewProject(string originalPath, string modifiedPath, string gitPath, ProjectType gameType);
        Project LoadProject(string projectFilePath);
        void SaveProject(Project project);
        void ScanGameResources(Project project);
        Task ScanGameResourcesAsync(Project project);
        void ReplaceResource(Project project, string gffFileName, ResourceInfo resource, byte[] newData);
        Task<Project> CreateNewProjectAsync(object originalGamePath, object modifiedGamePath, object gitRepositoryPath, object type);
        Task SaveProjectAsync(Project project);
    }
}
