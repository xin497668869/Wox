using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
{
    public static class StringMatcher
    {
        public static int Score(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                var matcher = FuzzyMatcher.Create(target);
                var score = matcher.Evaluate(source).Score;
                return score;
            }

            return 0;
        }


        public static int ScoreForPinyin(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if (source.Length > 40)
                {
                    Log.Debug($"|Wox.Infrastructure.StringMatcher.ScoreForPinyin|skip too long string: {source}");
                    return 0;
                }

                if (Alphabet.ContainsChinese(source))
                {
                    var matcher = FuzzyMatcher.Create(target);
                    var combination = Alphabet.PinyinComination(source);

                    var score = matcher.Evaluate(combination).Score;
                    return score;
                }

                return 0;
            }

            return 0;
        }

        public static string getPinyinString(string source)
        {
            if (Alphabet.ContainsChinese(source)) return Alphabet.PinyinComination(source);

            return source;
        }

        public static int ScoreForPinyinOrEng(string source, string target)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if (source.Length > 40)
                {
                    Log.Debug($"|Wox.Infrastructure.StringMatcher.ScoreForPinyin|skip too long string: {source}");
                    return 0;
                }

                if (Alphabet.ContainsChinese(source))
                {
                    var matcher = FuzzyMatcher.Create(target);
                    var combination = Alphabet.PinyinComination(source);

                    return matcher.Evaluate(combination).Score;
                }

                return Score(source, target);
            }

            return 0;
        }

        public static bool IsMatch(string source, string target)
        {
            return Score(source, target) > 0;
        }
    }
}