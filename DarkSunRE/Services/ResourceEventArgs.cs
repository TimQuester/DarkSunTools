using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class ResourceEventArgs : ProjectEventArgs
    {
        public ResourceInfo Resource { get; }
        public string GffFileName { get; }

        public ResourceEventArgs(ProjectAction action, Project project, ResourceInfo resource, string gffFileName)
            : base(action, project)
        {
            Resource = resource;
            GffFileName = gffFileName;
        }
    }
}
