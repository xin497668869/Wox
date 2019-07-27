using System;

namespace Wox.Infrastructure
{
    public class FilterScoreItem
    {
        private String path;
        private int score;

        public FilterScoreItem(string path, int score)
        {
            this.path = path;
            this.score = score;
        }

        public string Path
        {
            get => path;
            set => path = value;
        }

        public int Score
        {
            get => score;
            set => score = value;
        }
    }
}