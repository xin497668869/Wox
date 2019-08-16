using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;

namespace Wox.Plugin.Shell {
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable {
        private const string Image = "Images/shell.png";
        private readonly KeyboardSimulator _keyboardSimulator = new KeyboardSimulator(new InputSimulator());

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;
        private PluginInitContext _context;
        private bool _winRStroked;

        public Main() {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        public List<Result> LoadContextMenus(Result selectedResult) {
            return new List<Result> {
                new Result {
                    Title = _context.API.GetTranslation("wox_plugin_cmd_run_as_administrator"),
                    Action = c => {
                        Execute(selectedResult.Title, true);
                        return true;
                    },
                    IcoPath = Image
                }
            };
        }


        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            List<Result> results = new List<Result>();
            string cmd = query.Search;
            if (string.IsNullOrEmpty(cmd)) {
                return ResultsFromlHistory();
            }

            Result queryCmd = GetCurrentCmd(cmd);
            results.Add(queryCmd);
            List<Result> history = GetHistoryCmds(cmd, queryCmd);
            results.AddRange(history);

            try {
                string basedir = null;
                string dir = null;
                string excmd = Environment.ExpandEnvironmentVariables(cmd);
                if (Directory.Exists(excmd) && (cmd.EndsWith("/") || cmd.EndsWith(@"\"))) {
                    basedir = excmd;
                    dir = cmd;
                } else if (Directory.Exists(Path.GetDirectoryName(excmd) ?? string.Empty)) {
                    basedir = Path.GetDirectoryName(excmd);
                    string dirn = Path.GetDirectoryName(cmd);
                    dir = dirn.EndsWith("/") || dirn.EndsWith(@"\") ? dirn : cmd.Substring(0, dirn.Length + 1);
                }

                if (basedir != null) {
                    List<string> autocomplete = Directory.GetFileSystemEntries(basedir)
                        .Select(o => dir + Path.GetFileName(o)).Where(o =>
                            o.StartsWith(cmd, StringComparison.OrdinalIgnoreCase) &&
                            !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase)) &&
                            !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase))).ToList();
                    autocomplete.Sort();
                    results.AddRange(autocomplete.ConvertAll(m => new Result {
                        Title = m,
                        IcoPath = Image,
                        Action = c => {
                            Execute(m);
                            return true;
                        }
                    }));
                }
            } catch (Exception e) {
                Log.Exception($"|Wox.Plugin.Shell.Main.Query|Exception when query for <{query}>", e);
            }

            return results;
        }

        public void Init(PluginInitContext context) {
            _context = context;
            context.API.GlobalKeyboardEvent += API_GlobalKeyboardEvent;
        }

        public string GetTranslatedPluginTitle() {
            return _context.API.GetTranslation("wox_plugin_cmd_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return _context.API.GetTranslation("wox_plugin_cmd_plugin_description");
        }

        public void Save() {
            _storage.Save();
        }

        public Control CreateSettingPanel() {
            return new CMDSetting(_settings);
        }

        private List<Result> GetHistoryCmds(string cmd, Result result) {
            IEnumerable<Result> history = _settings.Count.Where(o => o.Key.Contains(cmd))
                .OrderByDescending(o => o.Value)
                .Select(m => {
                    if (m.Key == cmd) {
//                        result.SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value);
                        return null;
                    }

                    Result ret = new Result {
                        Title = m.Key,
//                        SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value),
                        IcoPath = Image,
                        Action = c => {
                            Execute(m.Key);
                            return true;
                        }
                    };
                    return ret;
                }).Where(o => o != null).Take(4);
            return history.ToList();
        }

        private Result GetCurrentCmd(string cmd) {
            Result result = new Result {
                Title = cmd,
                Score = 5000,
                SubTitle = _context.API.GetTranslation("wox_plugin_cmd_execute_through_shell"),
                IcoPath = Image,
                Action = c => {
                    Execute(cmd);
                    return true;
                }
            };

            return result;
        }

        private List<Result> ResultsFromlHistory() {
            IEnumerable<Result> history = _settings.Count.OrderByDescending(o => o.Value)
                .Select(m => new Result {
                    Title = m.Key,
                    SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"),
                        m.Value),
                    IcoPath = Image,
                    Action = c => {
                        Execute(m.Key);
                        return true;
                    }
                }).Take(5);
            return history.ToList();
        }

        private void Execute(string command, bool runAsAdministrator = false) {
            command = command.Trim();
            command = Environment.ExpandEnvironmentVariables(command);

            ProcessStartInfo info;
            if (_settings.Shell == Shell.Cmd) {
                string arguments = _settings.LeaveShellOpen ? $"/k \"{command}\"" : $"/c \"{command}\" & pause";
                info = new ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = arguments
                };
            } else if (_settings.Shell == Shell.Powershell) {
                string arguments;
                if (_settings.LeaveShellOpen) {
                    arguments = $"-NoExit \"{command}\"";
                } else {
                    arguments = $"\"{command} ; Read-Host -Prompt \\\"Press Enter to continue\\\"\"";
                }

                info = new ProcessStartInfo {
                    FileName = "powershell.exe",
                    Arguments = arguments
                };
            } else if (_settings.Shell == Shell.RunCommand) {
                string[] parts = command.Split(new[] {' '}, 2);
                if (parts.Length == 2) {
                    string filename = parts[0];
                    if (ExistInPath(filename)) {
                        string arguemtns = parts[1];
                        info = new ProcessStartInfo {
                            FileName = filename,
                            Arguments = arguemtns
                        };
                    } else {
                        info = new ProcessStartInfo(command);
                    }
                } else {
                    info = new ProcessStartInfo(command);
                }
            } else {
                return;
            }


            info.UseShellExecute = true;
            info.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            info.Verb = runAsAdministrator ? "runas" : "";

            try {
                Process.Start(info);
                _settings.AddCmdHistory(command);
            } catch (FileNotFoundException e) {
                MessageBox.Show($"Command not found: {e.Message}");
            }
        }

        private bool ExistInPath(string filename) {
            if (File.Exists(filename)) {
                return true;
            }

            string values = Environment.GetEnvironmentVariable("PATH");
            if (values != null) {
                foreach (string path in values.Split(';')) {
                    string path1 = Path.Combine(path, filename);
                    string path2 = Path.Combine(path, filename + ".exe");
                    if (File.Exists(path1) || File.Exists(path2)) {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state) {
            if (_settings.ReplaceWinR) {
                if (keyevent == (int) KeyEvent.WM_KEYDOWN && vkcode == (int) Keys.R && state.WinPressed) {
                    _winRStroked = true;
                    OnWinRPressed();
                    return false;
                }

                if (keyevent == (int) KeyEvent.WM_KEYUP && _winRStroked && vkcode == (int) Keys.LWin) {
                    _winRStroked = false;
                    _keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }

            return true;
        }

        private void OnWinRPressed() {
            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeywords[0]}{Plugin.Query.TermSeperater}");
            Application.Current.MainWindow.Visibility = Visibility.Visible;
        }
    }
}