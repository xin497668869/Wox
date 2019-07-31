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
        private const string ShortcutExtension = "lnk";
        private const string ApplicationReferenceExtension = "appref-ms";
        private const string ExeExtension = "exe";
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
                },
                new Result
                {
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        var hide = Main.StartProcess(new ProcessStartInfo(ParentDirectory));
                        return hide;
                    },
                    IcoPath = "Images/folder.png"
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

        private static Win32 LnkProgram(FilterScoreItem item)
        {
            var path = item.Path;
            var program = Win32Program(path);
            try
            {
//                var link = new ShellLink();
//                const uint STGM_READ = 0;
//                ((IPersistFile)link).Load(path, STGM_READ);
//                var hwnd = new _RemotableHandle();
//                link.Resolve(ref hwnd, 0);
//                const int MAX_PATH = 260;
//                StringBuilder buffer = new StringBuilder(MAX_PATH);
//
//                var data = new _WIN32_FIND_DATAW();
//                const uint SLGP_SHORTPATH = 1;
//                link.GetPath(buffer, buffer.Capacity, ref data, SLGP_SHORTPATH);
//                var target = buffer.ToString();
//                if (!string.IsNullOrEmpty(target))
//                {
//                    var extension = Extension(target);
//                    if (extension == ExeExtension && File.Exists(target))
//                    {
//                        Log.Info("|App.OnStartup|eeeeeee Wox saaaaatartup -------------------------------------5---------------"+path);
//                        buffer = new StringBuilder(MAX_PATH);
//                        link.GetDescription(buffer, MAX_PATH);
//                        var description = buffer.ToString();
//                        Log.Info("|App.OnStartup|eeeeeee Wox saaaaatartup -------------------------------------88--------------"+path);
//                        if (!string.IsNullOrEmpty(description))
//                        {
//                            program.Description = description;
//                        }
//                        else
//                        {
//                            Log.Info("|App.OnStartup|eeeeeee Wox saaaaatartup -----------------------------6-----------------------"+path);
//                            var info = FileVersionInfo.GetVersionInfo(target);
//                            if (!string.IsNullOrEmpty(info.FileDescription))
//                            {
//                                program.Description = info.FileDescription;
//                            }
//                        }
//                    }
//                }
//                Log.Info("|App.OnStartup|eeeeeee Wox saaaaatartup ----------------------------------------------------"+path);
                return program;
            }
            catch (COMException e)
            {
                // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always cause exception
                Log.Exception(
                    $"|Win32.LnkProgram|COMException when parsing shortcut <{path}> with HResult <{e.HResult}>", e);
                program.Valid = false;
                return program;
            }
            catch (Exception e)
            {
                Log.Exception($"|Win32.LnkProgram|Exception when parsing shortcut <{path}>", e);
                program.Valid = false;
                return program;
            }
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

        private static List<string> SearchCustomPathdPrograms(Settings settings)
        {
            var sources = settings.ProgramSources;
            var allSearchFile = new List<string>();
            var perSearchFile = new List<string>();
            var folderQueue = new Queue<Settings.ProgramSource>();

            foreach (var programSource in sources)
            {
                folderQueue.Enqueue(new Settings.ProgramSource(programSource.Location,
                    programSource.Priority,
                    1));

                while (folderQueue.Any())
                {
                    var parentDir = folderQueue.Dequeue();
                    if (parentDir.Deep > programSource.Deep)
                    {
                        allSearchFile.AddRange(perSearchFile);
                        perSearchFile.Clear();
                        folderQueue.Clear();
                        break;
                    }

                    var enumerateFiles = Directory.EnumerateFiles(programSource.Location,
                        "*",
                        SearchOption.TopDirectoryOnly);
                    perSearchFile.AddRange(enumerateFiles);

                    foreach (var directory in Directory.GetDirectories(programSource.Location))
                        folderQueue.Enqueue(new Settings.ProgramSource(directory,
                            parentDir.Priority,
                            programSource.Deep + 1));
                }
            }


            return allSearchFile;
        }


//        private static ParallelQuery<Win32> UnregisteredPrograms(List<Settings.ProgramSource> sources,
//            string[] suffixes)
//        {
//            var paths = sources.Where(s => Directory.Exists(s.Location))
//                .SelectMany(s => ProgramPaths(s.Location, suffixes))
//                .ToArray();
//            var programs1 = paths.AsParallel().Where(p => Extension(p) == ExeExtension).Select(ExeProgram);
//            var programs2 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(ExeProgram);
//            var programs3 = from p in paths.AsParallel()
//                let e = Extension(p)
//                where e != ShortcutExtension && e != ExeExtension
//                select Win32Program(p);
//            return programs1.Concat(programs2).Concat(programs3);
//        }
//        private static ParallelQuery<Win32> StartMenuPrograms(string[] suffixes)
//        {
//            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
//            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
//            var paths1 = ProgramPaths(directory1, suffixes);
//            var paths2 = ProgramPaths(directory2, suffixes);
//            var paths = paths1.Concat(paths2).ToArray();
//            var programs1 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(LnkProgram);
//            var programs2 = paths.AsParallel().Where(p => Extension(p) == ApplicationReferenceExtension).Select(Win32Program);
//            var programs = programs1.Concat(programs2).Where(p => p.Valid);
//            return programs;
//        }

        public static List<Win32> SearchPrograms(string searchText, Settings settings)
        {
            var sw = new Stopwatch();
            sw.Start();
            //搜索开始菜单
            var systemPathPrograms = ProgramPaths();
            //搜索自定义目录菜单
            var customPathPrograms = SearchCustomPathdPrograms(settings);

            customPathPrograms.AddRange(systemPathPrograms);

            var programs = customPathPrograms.Select(p =>
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
            var searchStartMenuPrograms = programs.Select(LnkProgram).ToList();
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

        private static ParallelQuery<Win32> AppPathsPrograms(string[] suffixes)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            var programs = new List<Win32>();
            using (var root = Registry.LocalMachine.OpenSubKey(appPaths))
            {
                if (root != null) programs.AddRange(ProgramsFromRegistryKey(root));
            }

            using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
            {
                if (root != null) programs.AddRange(ProgramsFromRegistryKey(root));
            }

            var filtered = programs.AsParallel().Where(p => suffixes.Contains(Extension(p.ExecutableName)));
            return filtered;
        }

        private static ParallelQuery<Win32> SearchAppPathsPrograms(string[] suffixes)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            var programs = new List<Win32>();
            using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
            {
                if (root != null) programs.AddRange(ProgramsFromRegistryKey(root));
            }

            var filtered = programs.AsParallel().Where(p => suffixes.Contains(Extension(p.ExecutableName)));
            return filtered;
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

        //private static Win32 ScoreFilter(Win32 p)
        //{
        //    var start = new[] { "启动", "start" };
        //    var doc = new[] { "帮助", "help", "文档", "documentation" };
        //    var uninstall = new[] { "卸载", "uninstall" };

        //    var contained = start.Any(s => p.Name.ToLower().Contains(s));
        //    if (contained)
        //    {
        //        p.Score += 10;
        //    }
        //    contained = doc.Any(d => p.Name.ToLower().Contains(d));
        //    if (contained)
        //    {
        //        p.Score -= 10;
        //    }
        //    contained = uninstall.Any(u => p.Name.ToLower().Contains(u));
        //    if (contained)
        //    {
        //        p.Score -= 20;
        //    }

        //    return p;
        //}
    }
}