using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.ViewModels
{
    public class ProjectCreatedEventArgs : EventArgs
    {
        public ProjectCreationData ProjectData { get; }

        public ProjectCreatedEventArgs(ProjectCreationData projectData)
        {
            ProjectData = projectData;
        }
    }
}
