using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.Programs {
    [Serializable]
    public class Win32 : IProgram {
        public string Name { get; set; }
        private string IcoPath { get; set; }
        public string FullPath { get; set; }
        private string ParentDirectory { get; set; }
        private string ExecutableName { get; set; }
        private string Description { get; set; }
        private bool Valid { get; set; }


        public Result Result(string query, IPublicAPI api) {
            Result result = new Result {
                SubTitle = FullPath,
                IcoPath = IcoPath,
                Score = Score(query),
                ContextData = this,
                Action = e => {
                    ProcessStartInfo info = new ProcessStartInfo {
                        FileName = FullPath,
                        WorkingDirectory = ParentDirectory
                    };
                    bool hide = Main.StartProcess(info);
                    return hide;
                }
            };

            if (Description.Length >= Name.Length &&
                Description.Substring(0, Name.Length) == Name) {
                result.Title = Description;
            } else if (!string.IsNullOrEmpty(Description)) {
                result.Title = $"{Name}: {Description}";
            } else {
                result.Title = Name;
            }

            return result;
        }


        public List<Result> ContextMenus(IPublicAPI api) {
            List<Result> contextMenus = new List<Result> {
                new Result {
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Action = _ => {
                        bool hide = Main.StartProcess(new ProcessStartInfo(ParentDirectory));
                        return hide;
                    },
                    IcoPath = "Images/folder.png"
                },
                new Result {
                    Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                    Action = _ => {
                        ProcessStartInfo info = new ProcessStartInfo {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas"
                        };
                        bool hide = Main.StartProcess(info);
                        return hide;
                    },
                    IcoPath = "Images/cmd.png"
                }
            };
            return contextMenus;
        }

        private int Score(string query) {
            int score1 = StringMatcher.Score(Name, query);
            int score2 = StringMatcher.ScoreForPinyin(Name, query);
            int score3 = StringMatcher.Score(Description, query);
            int score4 = StringMatcher.ScoreForPinyin(Description, query);
            int score5 = StringMatcher.Score(ExecutableName, query);
            int score = new[] {score1, score2, score3, score4, score5}.Max();
            return score;
        }


        public override string ToString() {
            return ExecutableName;
        }


        private static Win32 PathToWin32(FilterScoreItem item) {
            string path = item.Path;
            return new Win32 {
                Name = Path.GetFileNameWithoutExtension(path),
                IcoPath = path,
                FullPath = path,
                ParentDirectory = Directory.GetParent(path).FullName,
                Description = string.Empty,
                Valid = true
            };
        }


        private static List<Settings.ProgramSource> ProgramPaths() {
            List<Settings.ProgramSource> programSources = new List<Settings.ProgramSource>();
            programSources.Add(new Settings.ProgramSource(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                0,
                5));


            programSources.Add(new Settings.ProgramSource(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                0,
                5));
            return programSources;
        }


        private static List<string> SearchCustomPathPrograms(List<Settings.ProgramSource> programSources,
            bool containDir) {
            List<string> allSearchFile = new List<string>();
            Queue<Settings.ProgramSource> folderQueue = new Queue<Settings.ProgramSource>();
            IOrderedEnumerable<Settings.ProgramSource> sortSources = programSources.OrderByDescending(p => p.Priority);

            foreach (Settings.ProgramSource programSource in sortSources) {
                folderQueue.Enqueue(new Settings.ProgramSource(programSource.Location,
                    programSource.Priority,
                    1));

                while (folderQueue.Any()) {
                    Settings.ProgramSource parentDir = folderQueue.Dequeue();
                    if (containDir) {
                        allSearchFile.Add(parentDir.Location);
                    }

                    if (parentDir.Deep > programSource.Deep) {
                        if (containDir) {
                            allSearchFile.AddRange(folderQueue.Select(p => p.Location).ToList());
                        }

                        folderQueue.Clear();
                    }

                    IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(parentDir.Location,
                        "*",
                        SearchOption.TopDirectoryOnly);

                    allSearchFile.AddRange(enumerateFiles);

                    foreach (string directory in Directory.GetDirectories(parentDir.Location)) {
                        folderQueue.Enqueue(new Settings.ProgramSource(directory,
                            parentDir.Priority,
                            parentDir.Deep + 1));
                    }
                }
            }


            return allSearchFile;
        }


        public static IEnumerable<Win32> SearchPrograms(Query search, Settings settings,
            Dictionary<string, int> historyHistorySources) {
            //搜索自定义目录菜单
            List<string> searchPathPrograms = SearchCustomPathPrograms(settings.ProgramSources, true);
            //搜索开始菜单
            IEnumerable<string> systemPathPrograms = SearchCustomPathPrograms(ProgramPaths(), false);

            searchPathPrograms.AddRange(systemPathPrograms);

            List<FilterScoreItem> programs = searchPathPrograms.AsParallel().Distinct()
                .Select(p => {
                    XinFuzzyMatcher xinFuzzyMatcher = new XinFuzzyMatcher(Path.GetFileNameWithoutExtension(p));

                    xinFuzzyMatcher.init();
                    try {
                        bool isMatch = XinFuzzyMatcher.isMatch(xinFuzzyMatcher, search.Search, 0, 0);
                        if (!isMatch) {
                            return new FilterScoreItem(p, 0);
                        }

                        if (historyHistorySources != null && historyHistorySources.TryGetValue(p, out int score)) {
                            return new FilterScoreItem(p, score);
                        }

                        return new FilterScoreItem(p, 1000 - xinFuzzyMatcher.pinYinName.Length);
                    } catch (Exception e) {
                        Log.Error("" + e);
                    }

                    return new FilterScoreItem(p, 0);
                })
                .Where(p => p.Score > 0)
                .OrderByDescending(p => p.Score)
                .ToList();

            if (programs.Count > 20) {
                programs = programs.GetRange(0, 20);
            }


            List<Win32> searchStartMenuPrograms = programs.Select(PathToWin32).ToList();
            return searchStartMenuPrograms;
        }
    }
}