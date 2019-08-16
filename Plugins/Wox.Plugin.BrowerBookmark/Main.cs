using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wox.Infrastructure;

namespace Wox.Plugin.BrowerBookmark {
    public class Main : IPlugin, IPluginI18n {
        private PluginInitContext Context { get; set; }


        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            string chromeDatePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) +
                                    @"\Google\Chrome\User Data\Default";
            string readAllText = File.ReadAllText(chromeDatePath + @"\Bookmarks");
            Bookmark deserializeObject = JsonConvert.DeserializeObject<Bookmark>(readAllText);
            List<Bookmark.Mark> searchMarks = new List<Bookmark.Mark>();

            searchBookmark(searchMarks, deserializeObject.roots.bookmark_bar, query.Search, "");

            searchBookmark(searchMarks, deserializeObject.roots.other, query.Search, "");
            searchBookmark(searchMarks, deserializeObject.roots.synced, query.Search, "");

            List<Result> results = searchMarks.OrderByDescending(p => {
                historyHistorySources.TryGetValue(p.url, out int score);
                return score;
            }).Take(100).Select(p => {
                Result result = new Result();
                result.Title = p.name;
                result.SubTitle = p.folderPath;
                result.IcoPath = chromeDatePath + @"\Google Profile.ico";
                result.Action = context => {
                    Process.Start(p.url);
                    return true;
                };
                result.historySave = p.url;
                return result;
            }).ToList();

            return results;
        }


        public void Init(PluginInitContext context) {
            Context = context;
            JsonConvert.DeserializeObject<Bookmark>("{}");
        }

        public string GetTranslatedPluginTitle() {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_description");
        }


        public void searchBookmark(List<Bookmark.Mark> searchMarks, Bookmark.Mark mark,
            string queryString, string foldPath) {
            if (mark.type.Equals("folder")) {
                foreach (Bookmark.Mark markChild in mark.children) {
                    searchBookmark(searchMarks, markChild, queryString, foldPath + "/" + mark.name);
                }
            } else if (mark.type.Equals("url")) {
                if (XinFuzzyMatcher.isMatch(mark.name, queryString)) {
                    mark.folderPath = foldPath;
                    searchMarks.Add(mark);
                }
            }
        }

        public class Bookmark {
            public string checksum { get; set; }
            public Root roots { get; set; }

            public class Root {
                public Mark bookmark_bar { get; set; }
                public Mark other { get; set; }
                public string sync_transaction_version { get; set; }
                public Mark synced { get; set; }
            }

            public class Mark {
                public List<Mark> children { get; set; }
                public string date_added { get; set; }
                public string folderPath { get; set; }
                public string date_modified { get; set; }
                public string name { get; set; }
                public string id { get; set; }
                public string url { get; set; }
                public string type { get; set; }
            }
        }
    }
}