namespace Wox.Plugin.Everything.Everything
{
    public class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }

        public long Size { get; set; }
        public long DateCreated { get; set; }
        public long DateModified { get; set; }
    }
}