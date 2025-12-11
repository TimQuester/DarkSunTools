namespace DarkSunRE.Services
{
    public class CommitEventArgs : EventArgs
    {
        public CommitInfo Commit { get; }
        public bool Success { get; }

        public CommitEventArgs(CommitInfo commit, bool success)
        {
            Commit = commit;
            Success = success;
        }
    }
}
