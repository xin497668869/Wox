namespace Wox.Infrastructure
{
    public class FilterScoreItem
    {
        public FilterScoreItem(string path, double score)
        {
            Path = path;
            Score = score;
        }

        public string Path { get; set; }

        public double Score { get; set; }
    }
}