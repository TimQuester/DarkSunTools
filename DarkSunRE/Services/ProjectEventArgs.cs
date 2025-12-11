using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class ProjectEventArgs : EventArgs
    {
        public ProjectAction Action { get; }
        public Project Project { get; }
        public DateTime EventTime { get; }

        public ProjectEventArgs(ProjectAction action, Project project)
        {
            Action = action;
            Project = project;
            EventTime = DateTime.UtcNow;
        }
    }
}
