using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public interface IGitService
    {
        Task<List<CommitInfo>> GetCommitHistoryAsync(int maxCount = 50);
        Task<CommitInfo> GetCommitAsync(string sha);
        Task<List<FileChangeInfo>> GetCommitFileChangesAsync(string sha);
        Task<CommitStatistics> GetCommitStatisticsAsync(DateTime? since = null, DateTime? until = null);

        Task<bool> CommitChangesAsync(string message, CommitType commitType = CommitType.ResourceEdit);
        Task<bool> RevertCommitAsync(string sha);
        Task<bool> CreateBranchAsync(string branchName);
        Task<List<string>> GetBranchesAsync();

        event EventHandler<CommitEventArgs> CommitPerformed;
        event EventHandler<BranchEventArgs> BranchChanged;
    }
}
