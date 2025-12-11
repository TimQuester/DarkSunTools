using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class GitOperationEventArgs : ProjectEventArgs
    {
        public string CommitMessage { get; }
        public string BranchName { get; }
        public int FilesChanged { get; }

        public GitOperationEventArgs(ProjectAction action, Project project, string commitMessage = null, string branchName = null, int filesChanged = 0)
            : base(action, project)
        {
            CommitMessage = commitMessage;
            BranchName = branchName;
            FilesChanged = filesChanged;
        }
    }
}
