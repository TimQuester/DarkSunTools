using DarkSunRE.Models;
using DarkSunRE.Services;

namespace DarkSunRE.ViewModels
{
    public class ResourceEventArgs : EventArgs
    {
        public ResourceInfo Resource { get; }
        public ProjectAction Action { get; }
        public Project Project { get; }
        public string GffFileName { get; }

        public ResourceEventArgs(ProjectAction action, Project project, ResourceInfo resource, string gffFileName)
        {
            Resource = resource;
            GffFileName = gffFileName;
            Project = project;
            Action = action;
        }
    }
}
 