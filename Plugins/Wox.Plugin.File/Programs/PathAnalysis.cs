using System.IO;
using Wox.Infrastructure;

namespace Wox.Plugin.Program.Programs
{
    public class PathAnalysis
    {
        public PathAnalysis(string path)
        {
            this.path = path;
            fileName = Path.GetFileNameWithoutExtension(path);
        }

        public string fileName { get; set; }
        public string pinYinName { get; set; }
        public string path { get; set; }

        public void init()
        {
            var start = 0;
            pinYinName = Alphabet.PinyinComination(fileName);
        }
    }
}