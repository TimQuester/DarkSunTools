namespace DarkSunRE.Services
{
    public class CommitStatistics
    {
        public int TotalCommits { get; set; }
        public int TotalFilesChanged { get; set; }
        public int TotalInsertions { get; set; }
        public int TotalDeletions { get; set; }
        public DateTime FirstCommit { get; set; }
        public DateTime LastCommit { get; set; }
        public Dictionary<string, int> ChangesByType { get; set; }
    }
}
