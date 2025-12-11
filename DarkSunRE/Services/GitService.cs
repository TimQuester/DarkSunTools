using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class GitService : IGitService
    {
        public event EventHandler<CommitEventArgs> CommitPerformed;
        public event EventHandler<BranchEventArgs> BranchChanged;

        public Task<bool> CommitChangesAsync(string message, CommitType commitType = CommitType.ResourceEdit)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CreateBranchAsync(string branchName)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetBranchesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<CommitInfo> GetCommitAsync(string sha)
        {
            throw new NotImplementedException();
        }

        public Task<List<FileChangeInfo>> GetCommitFileChangesAsync(string sha)
        {
            throw new NotImplementedException();
        }

        public async Task<List<CommitInfo>> GetCommitHistoryAsync(int maxCount = 50)
        {
            throw new NotImplementedException();
        }

        public Task<CommitStatistics> GetCommitStatisticsAsync(DateTime? since = null, DateTime? until = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RevertCommitAsync(string sha)
        {
            throw new NotImplementedException();
        }
    }
}
