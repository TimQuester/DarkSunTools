using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class GffFileEventArgs : ProjectEventArgs
    {
        public GffFileInfo GffFile { get; }
        public string[] ModifiedResources { get; }

        public GffFileEventArgs(ProjectAction action, Project project, GffFileInfo gffFile, string[] modifiedResources = null)
            : base(action, project)
        {
            GffFile = gffFile;
            ModifiedResources = modifiedResources ?? Array.Empty<string>();
        }
    }
}
