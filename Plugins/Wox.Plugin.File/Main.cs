using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program {
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable {
        private static readonly object IndexLock = new object();
        private static Win32[] _win32s;

        private static PluginInitContext _context;

        private static BinaryStorage<Win32[]> _win32Storage;
        private static Settings _settings;
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main() {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Normal("|Wox.Plugin.Program.Main|Preload programs cost", () => {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
            });
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
        }

        public List<Result> LoadContextMenus(Result selectedResult) {
            IProgram program = selectedResult.ContextData as IProgram;
            if (program != null) {
                List<Result> menus = program.ContextMenus(_context.API);
                return menus;
            }

            return new List<Result>();
        }

        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            lock (IndexLock) {
                IEnumerable<Win32> programs = Win32.SearchPrograms(query, _settings, historyHistorySources);

                List<Result> results = programs.Select(p => {
                    Result result1 = p.Result(query.Search, _context.API);
                    result1.historySave = p.FullPath;
                    return result1;
                }).ToList();

                return results;
            }
        }

        public void Init(PluginInitContext context) {
            _context = context;
        }

        public string GetTranslatedPluginTitle() {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public void Save() {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
        }

        public Control CreateSettingPanel() {
            return new ProgramSetting(_context, _settings);
        }

        public static bool StartProcess(ProcessStartInfo info) {
            bool hide;
            try {
                Process.Start(info);
                hide = true;
            } catch (Exception) {
                string name = "Plugin: Program";
                string message = $"Can't start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
                hide = false;
            }

            return hide;
        }
    }
}