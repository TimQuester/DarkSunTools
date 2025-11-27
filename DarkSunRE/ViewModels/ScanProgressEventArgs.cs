namespace DarkSunRE.ViewModels
{
    internal class ScanProgressEventArgs
    {
        public int TotalFiles;
        public string FilesProcessed;
        public int ProgressPercentage;
        internal object CurrentOperation;
    }
}