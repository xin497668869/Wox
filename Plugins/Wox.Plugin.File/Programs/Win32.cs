using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Wox.Plugin.Program.Programs
{
    [Serializable]
    public class Win32 : IProgram
    {
        public string Name { get; set; }
        public string IcoPath { get; set; }
        public string FullPath { get; set; }
        public string ParentDirectory { get; set; }
        public string ExecutableName { get; set; }
        public string Description { get; set; }
        public bool Valid { get; set; }


        public Result Result(string query, IPublicAPI api)
        {
            var result = new Result
            {
                SubTitle = FullPath,
                IcoPath = IcoPath,
                Score = Score(query),
                ContextData = this,
                Action = e =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = FullPath,
                        WorkingDirectory = ParentDirectory
                    };
                    var hide = Main.StartProcess(info);
                    return hide;
                }
            };

            if (Description.Length >= Name.Length &&
                Description.Substring(0, Name.Length) == Name)
                result.Title = Description;
            else if (!string.IsNullOrEmpty(Description))
                result.Title = $"{Name}: {Description}";
            else
                result.Title = Name;

            return result;
        }


        public List<Result> ContextMenus(IPublicAPI api)
        {
            var contextMenus = new List<Result>
            {
                new Result
                {
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        var hide = Main.StartProcess(new ProcessStartInfo(ParentDirectory));
                        return hide;
                    },
                    IcoPath = "Images/folder.png"
                },
                new Result
                {
                    Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas"
                        };
                        var hide = Main.StartProcess(info);
                        return hide;
                    },
                    IcoPath = "Images/cmd.png"
                }
            };
            return contextMenus;
        }

        private int Score(string query)
        {
            var score1 = StringMatcher.Score(Name, query);
            var score2 = StringMatcher.ScoreForPinyin(Name, query);
            var score3 = StringMatcher.Score(Description, query);
            var score4 = StringMatcher.ScoreForPinyin(Description, query);
            var score5 = StringMatcher.Score(ExecutableName, query);
            var score = new[] {score1, score2, score3, score4, score5}.Max();
            return score;
        }


        public override string ToString()
        {
            return ExecutableName;
        }

        private static Win32 Win32Program(string path)
        {
            var p = new Win32
            {
                Name = Path.GetFileNameWithoutExtension(path),
                IcoPath = path,
                FullPath = path,
                ParentDirectory = Directory.GetParent(path).FullName,
                Description = string.Empty,
                Valid = true
            };
            return p;
        }

        private static Win32 PathToWin32(FilterScoreItem item)
        {
            var path = item.Path;
            return new Win32
            {
                Name = Path.GetFileNameWithoutExtension(path),
                IcoPath = path,
                FullPath = path,
                ParentDirectory = Directory.GetParent(path).FullName,
                Description = string.Empty,
                Valid = true
            };
        }

        private static Win32 ExeProgram(string path)
        {
            var program = Win32Program(path);
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrEmpty(info.FileDescription)) program.Description = info.FileDescription;

            return program;
        }

        private static IEnumerable<string> ProgramPaths()
        {
            var directory = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (!Directory.Exists(directory))
                return new string[] { };

            return Directory.EnumerateFiles(directory, "*",
                SearchOption.AllDirectories);
        }


        private static string Extension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLower();
            if (!string.IsNullOrEmpty(extension))
                return extension.Substring(1);
            return string.Empty;
        }

        private static List<string> SearchCustomPathPrograms(Settings settings)
        {
            var sources = settings.ProgramSources;
            var allSearchFile = new List<string>();
            var folderQueue = new Queue<Settings.ProgramSource>();
            var sortSources = sources.OrderByDescending(p => p.Priority);

            foreach (var programSource in sortSources)
            {
                folderQueue.Enqueue(new Settings.ProgramSource(programSource.Location,
                    programSource.Priority,
                    1));

                while (folderQueue.Any())
                {
                    var parentDir = folderQueue.Dequeue();
                    allSearchFile.Add(parentDir.Location);
                    if (parentDir.Deep > programSource.Deep)
                    {
                        allSearchFile.AddRange(folderQueue.Select(p => p.Location).ToList());
                        folderQueue.Clear();
                        break;
                    }

                    var enumerateFiles = Directory.EnumerateFiles(programSource.Location,
                        "*",
                        SearchOption.TopDirectoryOnly);
                    allSearchFile.AddRange(enumerateFiles);

                    foreach (var directory in Directory.GetDirectories(programSource.Location))
                        folderQueue.Enqueue(new Settings.ProgramSource(directory,
                            parentDir.Priority,
                            programSource.Deep + 1));
                }
            }


            return allSearchFile;
        }


        public static List<Win32> SearchPrograms(string searchText, Settings settings)
        {
            var sw = new Stopwatch();
            sw.Start();

            //搜索自定义目录菜单
            var customPathPrograms = SearchCustomPathPrograms(settings);

            //搜索开始菜单
            var systemPathPrograms = ProgramPaths();

            customPathPrograms.AddRange(systemPathPrograms);

            var programs = customPathPrograms.AsParallel().Distinct()
                .Select(p =>
                {
                    var pathAnalysis = new PathAnalysis(p);

                    pathAnalysis.init();
                    try
                    {
                        var score = isMatch(pathAnalysis, searchText, 0, 0);
                        return new FilterScoreItem(p, score ? 1000 - pathAnalysis.pinYinName.Length : 0);
                    }
                    catch (Exception e)
                    {
                        Log.Error("" + e);
                    }

                    return new FilterScoreItem(p, 0);
                })
                .Where(p => p.Score > 0)
                .OrderByDescending(p => p.Score)
                .ToList();

            if (programs.Count > 20) programs = programs.GetRange(0, 20);


//            var programs = programs1.Concat(programs2).Where(p => p.Valid);
            var searchStartMenuPrograms = programs.Select(PathToWin32).ToList();
            return searchStartMenuPrograms;
        }

        public static char getLowerChar(char source)
        {
            if (source <= 'Z' && source >= 'A') source = (char) (source + 'a' - 'A');

            return source;
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

        private static IEnumerable<Win32> ProgramsFromRegistryKey(RegistryKey root)
        {
            var programs = root.GetSubKeyNames()
                .Select(subkey => ProgramFromRegistrySubkey(root, subkey))
                .Where(p => !string.IsNullOrEmpty(p.Name));
            return programs;
        }

        private static Win32 ProgramFromRegistrySubkey(RegistryKey root, string subkey)
        {
            using (var key = root.OpenSubKey(subkey))
            {
                if (key != null)
                {
                    var defaultValue = string.Empty;
                    var path = key.GetValue(defaultValue) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        // fix path like this: ""\"C:\\folder\\executable.exe\""
                        path = path.Trim('"', ' ');
                        path = Environment.ExpandEnvironmentVariables(path);

                        if (File.Exists(path))
                        {
                            var entry = Win32Program(path);
                            entry.ExecutableName = subkey;
                            return entry;
                        }

                        return new Win32();
                    }

                    return new Win32();
                }

                return new Win32();
            }
        }
    }
}