using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Everything.Everything;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Wox.Plugin.Everything {
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable {
        public const string DLL = "Everything.dll";
        private readonly EverythingAPI _api = new EverythingAPI();

        private PluginInitContext _context;

        private Settings _settings;
        private PluginJsonStorage<Settings> _storage;

        public List<Result> LoadContextMenus(Result selectedResult) {
            SearchResult record = selectedResult.ContextData as SearchResult;
            List<Result> contextMenus = new List<Result>();
            if (record == null) {
                return contextMenus;
            }

            List<ContextMenu> availableContextMenus = new List<ContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());
            availableContextMenus.AddRange(_settings.ContextMenus);

            if (record.Type == ResultType.File) {
                foreach (ContextMenu contextMenu in availableContextMenus) {
                    ContextMenu menu = contextMenu;
                    contextMenus.Add(new Result {
                        Title = contextMenu.Name,
                        Action = _ => {
                            string argument = menu.Argument.Replace("{path}", record.FullPath);
                            try {
                                Process.Start(menu.Command, argument);
                            } catch {
                                _context.API.ShowMsg(
                                    string.Format(_context.API.GetTranslation("wox_plugin_everything_canot_start"),
                                        record.FullPath), string.Empty, string.Empty);
                                return false;
                            }

                            return true;
                        },
                        IcoPath = contextMenu.ImagePath
                    });
                }
            }

            string icoPath = record.Type == ResultType.File ? "Images\\file.png" : "Images\\folder.png";
            contextMenus.Add(new Result {
                Title = _context.API.GetTranslation("wox_plugin_everything_copy_path"),
                Action = context => {
                    Clipboard.SetText(record.FullPath);
                    return true;
                },
                IcoPath = icoPath
            });

            contextMenus.Add(new Result {
                Title = _context.API.GetTranslation("wox_plugin_everything_copy"),
                Action = context => {
                    Clipboard.SetFileDropList(new StringCollection {record.FullPath});
                    return true;
                },
                IcoPath = icoPath
            });

            if (record.Type == ResultType.File || record.Type == ResultType.Folder) {
                contextMenus.Add(new Result {
                    Title = _context.API.GetTranslation("wox_plugin_everything_delete"),
                    Action = context => {
                        try {
                            if (record.Type == ResultType.File) {
                                File.Delete(record.FullPath);
                            } else {
                                Directory.Delete(record.FullPath);
                            }
                        } catch {
                            _context.API.ShowMsg(
                                string.Format(_context.API.GetTranslation("wox_plugin_everything_canot_delete"),
                                    record.FullPath), string.Empty, string.Empty);
                            return false;
                        }

                        return true;
                    },
                    IcoPath = icoPath
                });
            }

            return contextMenus;
        }

        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            List<Result> results = new List<Result>();
            if (!string.IsNullOrEmpty(query.Search)) {
                string keyword = query.Search;

                try {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    List<SearchResult> searchList = _api.Search(keyword, maxCount: _settings.MaxSearchCount).ToList();
                    Log.Warn("===" + sw.ElapsedMilliseconds);
                    foreach (SearchResult s in searchList) {
                        string path = s.FullPath;

                        string workingDir = null;
                        if (_settings.UseLocationAsWorkingDir) {
                            workingDir = Path.GetDirectoryName(path);
                        }

                        Result r = new Result();
                        r.Title = Path.GetFileName(path);
                        string modifiedDate = s.DateModified != 0
                            ? DateTime.FromFileTime(s.DateModified).ToString("yyyy-MM-dd HH:mm:ss")
                            : "                   ";
                        string createdDate = s.DateCreated != 0
                            ? DateTime.FromFileTime(s.DateCreated).ToString("yyyy-MM-dd HH:mm:ss")
                            : "                   ";
                        r.SubTitle = path + " \n" + getSizeFormat(s.Size) + "\t\t" + modifiedDate + "\t" +
                                     createdDate;
                        r.IcoPath = path;
                        r.Action = c => {
                            bool hide;
                            try {
                                Process.Start(new ProcessStartInfo {
                                    FileName = path,
                                    UseShellExecute = true,
                                    WorkingDirectory = workingDir
                                });
                                hide = true;
                            } catch (Win32Exception) {
                                string name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                                string message = "Can't open this file";
                                _context.API.ShowMsg(name, message, string.Empty);
                                hide = false;
                            }

                            return hide;
                        };
                        r.ContextData = s;
                        results.Add(r);
                    }

                    Log.Warn("===22 " + sw.ElapsedMilliseconds);
                } catch (IPCErrorException) {
                    results.Add(new Result {
                        Title = _context.API.GetTranslation("wox_plugin_everything_is_not_running"),
                        IcoPath = "Images\\warning.png"
                    });
                } catch (Exception e) {
                    results.Add(new Result {
                        Title = _context.API.GetTranslation("wox_plugin_everything_query_error"),
                        SubTitle = e.Message,
                        Action = _ => {
                            Clipboard.SetText(e.Message + "\r\n" + e.StackTrace);
                            _context.API.ShowMsg(_context.API.GetTranslation("wox_plugin_everything_copied"), null,
                                string.Empty);
                            return false;
                        },
                        IcoPath = "Images\\error.png"
                    });
                }
            }

            _api.Reset();

            return results;
        }

        public void Init(PluginInitContext context) {
            _context = context;
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();

            string pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            const string sdk = "EverythingSDK";
            string bundledSDKDirectory = Path.Combine(pluginDirectory, sdk, CpuType());
            string sdkDirectory = Path.Combine(_storage.DirectoryPath, sdk, CpuType());
            Helper.ValidateDataDirectory(bundledSDKDirectory, sdkDirectory);

            string sdkPath = Path.Combine(sdkDirectory, DLL);
            Constant.EverythingSDKPath = sdkPath;
            LoadLibrary(sdkPath);
//            _api.init();
        }

        public string GetTranslatedPluginTitle() {
            return _context.API.GetTranslation("wox_plugin_everything_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return _context.API.GetTranslation("wox_plugin_everything_plugin_description");
        }

        public void Save() {
            _storage.Save();
        }

        public Control CreateSettingPanel() {
            return new EverythingSettings(_settings);
        }

        [DllImport("kernel32.dll")]
        private static extern int LoadLibrary(string name);

        private List<ContextMenu> GetDefaultContextMenu() {
            List<ContextMenu> defaultContextMenus = new List<ContextMenu>();
            ContextMenu openFolderContextMenu = new ContextMenu {
                Name = _context.API.GetTranslation("wox_plugin_everything_open_containing_folder"),
                Command = "explorer.exe",
                Argument = " /select,\"{path}\"",
                ImagePath = "Images\\folder.png"
            };

            defaultContextMenus.Add(openFolderContextMenu);

            string editorPath = string.IsNullOrEmpty(_settings.EditorPath) ? "notepad.exe" : _settings.EditorPath;

            ContextMenu openWithEditorContextMenu = new ContextMenu {
                Name = string.Format(_context.API.GetTranslation("wox_plugin_everything_open_with_editor"),
                    Path.GetFileNameWithoutExtension(editorPath)),
                Command = editorPath,
                Argument = " \"{path}\"",
                ImagePath = editorPath
            };

            defaultContextMenus.Add(openWithEditorContextMenu);

            return defaultContextMenus;
        }

        public string getSizeFormat(long size) {
            if (size < 1024) {
                return size + "B";
            }

            if (size < 1048576) {
                return size / 1024 + "KB";
            }

            if (size < 1073741824) {
                return size / 1048576 + "MB";
            }

            if (size < 1099511627776) {
                return size / 1073741824 + "GB";
            }

            return "-1";
        }

        private static string CpuType() {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }
    }
}