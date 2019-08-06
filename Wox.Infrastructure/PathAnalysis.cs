using System.IO;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
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

        public static bool isMatch(string content, string searchText)
        {
            try
            {
                var pathAnalysis = new PathAnalysis(content);
                pathAnalysis.init();
                var match = isMatch(pathAnalysis,
                    searchText, 0, 0);
                return match;
            }
            catch (System.Exception e)
            {
                Log.Error(""+e);
            }

            return false;
        }


        public static bool isMatch(PathAnalysis content, string query, int queryIndex, int contentIndex)
        {
            if (queryIndex >= query.Length) return true;

            var isMatchUpper = false;
            for (var j = contentIndex; j < content.pinYinName.Length; j++)
            {
                //如果前一个已经匹配了大写后剩余小写字符, 则需要过滤非小写的字符
                if (content.pinYinName[j] >= 'A' && content.pinYinName[j] <= 'Z') isMatchUpper = true;

                if (isMatchUpper && content.pinYinName[j] >= 'A' && content.pinYinName[j] <= 'Z'
                    || !isMatchUpper)
                    if (getLowerChar(content.pinYinName[j]) == getLowerChar(query[queryIndex]))
                        if (isMatch(content, query, queryIndex + 1, j + 1))
                            return true;
            }


            return false;
        }


        public static char getLowerChar(char source)
        {
            if (source <= 'Z' && source >= 'A') source = (char) (source + 'a' - 'A');

            return source;
        }
    }
}