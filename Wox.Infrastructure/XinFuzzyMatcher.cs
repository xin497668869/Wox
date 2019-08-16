using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure {
    public class XinFuzzyMatcher {
        public XinFuzzyMatcher(string fileName) {
            this.fileName = fileName;
        }

        public string fileName { get; set; }
        public string pinYinName { get; set; }

        public void init() {
            pinYinName = Alphabet.PinyinComination(fileName);
        }

        public static bool isMatch(string content, string searchText) {
            try {
                XinFuzzyMatcher pathAnalysis = new XinFuzzyMatcher(content);
                pathAnalysis.init();
                bool match = isMatch(pathAnalysis,
                    searchText, 0, 0);
                return match;
            } catch (System.Exception e) {
                Log.Error("" + e);
            }

            return false;
        }


        public static bool isMatch(XinFuzzyMatcher content, string query, int queryIndex, int contentIndex) {
            if (queryIndex >= query.Length) {
                return true;
            }

            bool isMatchUpper = false;
            for (int j = contentIndex; j < content.pinYinName.Length; j++) {
                //如果前一个已经匹配了大写后剩余小写字符, 则需要过滤非小写的字符
                if (content.pinYinName[j] >= 'A' && content.pinYinName[j] <= 'Z') {
                    isMatchUpper = true;
                }

                if (isMatchUpper && content.pinYinName[j] >= 'A' && content.pinYinName[j] <= 'Z'
                    || !isMatchUpper) {
                    if (getLowerChar(content.pinYinName[j]) == getLowerChar(query[queryIndex])) {
                        if (isMatch(content, query, queryIndex + 1, j + 1)) {
                            return true;
                        }
                    }
                }
            }


            return false;
        }


        public static char getLowerChar(char source) {
            if (source <= 'Z' && source >= 'A') {
                source = (char) (source + 'a' - 'A');
            }

            return source;
        }
    }
}