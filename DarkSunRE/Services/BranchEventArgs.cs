namespace DarkSunRE.Services
{
    public class BranchEventArgs : EventArgs
    {
        public string BranchName { get; }
        public BranchAction Action { get; }

        public BranchEventArgs(string branchName, BranchAction action)
        {
            BranchName = branchName;
            Action = action;
        }
    }
}
