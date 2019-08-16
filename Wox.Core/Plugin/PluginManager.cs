using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Plugin {
    /// <summary>
    ///     The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager {
        private static IEnumerable<PluginPair> _contextMenuPlugins;
        public static readonly List<PluginPair> GlobalPlugins = new List<PluginPair>();
        public static readonly Dictionary<string, PluginPair> NonGlobalPlugins = new Dictionary<string, PluginPair>();

        // todo happlebao, this should not be public, the indicator function should be embeded
        public static PluginsSettings Settings;
        private static List<PluginMetadata> _metadatas;
        private static readonly string[] Directories = {Constant.PreinstalledDirectory, Constant.PluginsDirectory};

        static PluginManager() {
            ValidateUserDirectory();
            // force old plugins use new python binding
            DeletePythonBinding();
        }

        /// <summary>
        ///     Directories that will hold Wox plugin directory
        /// </summary>

        public static List<PluginPair> AllPlugins { get; private set; }

        public static IPublicAPI API { private set; get; }

        private static void ValidateUserDirectory() {
            if (!Directory.Exists(Constant.PluginsDirectory)) {
                Directory.CreateDirectory(Constant.PluginsDirectory);
            }
        }

        private static void DeletePythonBinding() {
            const string binding = "wox.py";
            string directory = Constant.PluginsDirectory;
            foreach (string subDirectory in Directory.GetDirectories(directory)) {
                string path = Path.Combine(subDirectory, binding);
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }

        public static void Save() {
            foreach (PluginPair plugin in AllPlugins) {
                ISavable savable = plugin.Plugin as ISavable;
                savable?.Save();
            }
        }

        /// <summary>
        ///     because InitializePlugins needs API, so LoadPlugins needs to be called first
        ///     todo happlebao The API should be removed
        /// </summary>
        /// <param name="settings"></param>
        public static void LoadPlugins(PluginsSettings settings) {
            _metadatas = PluginConfig.Parse(Directories);
            Settings = settings;
            Settings.UpdatePluginSettings(_metadatas);
            AllPlugins = PluginsLoader.Plugins(_metadatas, Settings);
        }

        public static void InitializePlugins(IPublicAPI api) {
            API = api;
            Parallel.ForEach(AllPlugins, pair => {
                long milliseconds = Stopwatch.Debug(
                    $"|PluginManager.InitializePlugins|Init method time cost for <{pair.Metadata.Name}>", () => {
                        pair.Plugin.Init(new PluginInitContext {
                            CurrentPluginMetadata = pair.Metadata,
                            API = API
                        });
                    });
                pair.Metadata.InitTime += milliseconds;
                Log.Info(
                    $"|PluginManager.InitializePlugins|Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>");
            });

            _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();
            foreach (PluginPair plugin in AllPlugins) {
                if (IsGlobalPlugin(plugin.Metadata)) {
                    GlobalPlugins.Add(plugin);
                } else {
                    foreach (string actionKeyword in plugin.Metadata.ActionKeywords) {
                        NonGlobalPlugins[actionKeyword] = plugin;
                    }
                }
            }
        }

        public static void InstallPlugin(string path) {
            PluginInstaller.Install(path);
        }

        public static Query QueryInit(string text) //todo is that possible to move it into type Query?
        {
            // replace multiple white spaces with one white space
            string[] terms = text.Split(new[] {Query.TermSeperater}, StringSplitOptions.RemoveEmptyEntries);
            string rawQuery = string.Join(Query.TermSeperater, terms);
            string actionKeyword = string.Empty;
            string search = rawQuery;
            List<string> actionParameters = terms.ToList();
            if (terms.Length == 0) {
                return null;
            }

            if (NonGlobalPlugins.ContainsKey(terms[0]) &&
                !Settings.Plugins[NonGlobalPlugins[terms[0]].Metadata.ID].Disabled) {
                actionKeyword = terms[0];
                actionParameters = terms.Skip(1).ToList();
                search = string.Join(Query.TermSeperater, actionParameters.ToArray());
            }

            Query query = new Query {
                Terms = terms,
                RawQuery = rawQuery,
                ActionKeyword = actionKeyword,
                Search = search,
                // Obsolete value initialisation
                ActionName = actionKeyword,
                ActionParameters = actionParameters
            };
            return query;
        }

        public static List<PluginPair> ValidPluginsForQuery(Query query) {
            if (NonGlobalPlugins.ContainsKey(query.ActionKeyword)) {
                PluginPair plugin = NonGlobalPlugins[query.ActionKeyword];
                return new List<PluginPair> {plugin};
            }

            return GlobalPlugins;
        }

        public static List<Result> QueryForPlugin(PluginPair pair, Query query,
            Dictionary<string, int> historyHistorySources) {
            List<Result> results = new List<Result>();
            try {
                PluginMetadata metadata = pair.Metadata;
                long milliseconds = Stopwatch.Debug($"|PluginManager.QueryForPlugin|Cost for {metadata.Name}", () => {
                    results = pair.Plugin.Query(query, historyHistorySources) ?? results;
                    UpdatePluginMetadata(results, metadata, query);
                });
                metadata.QueryCount += 1;
                metadata.AvgQueryTime =
                    metadata.QueryCount == 1 ? milliseconds : (metadata.AvgQueryTime + milliseconds) / 2;
                return results;
            } catch (Exception e) {
                Log.Exception(
                    $"|PluginManager.QueryForPlugin|Exception for plugin <{pair.Metadata.Name}> when query <{query}>",
                    e);
                return new List<Result>();
            }
        }

        public static void UpdatePluginMetadata(List<Result> results, PluginMetadata metadata, Query query) {
            foreach (Result r in results) {
                r.PluginDirectory = metadata.PluginDirectory;
                r.PluginID = metadata.ID;
                r.OriginQuery = query;
            }
        }

        private static bool IsGlobalPlugin(PluginMetadata metadata) {
            return metadata.ActionKeywords.Contains(Query.GlobalPluginWildcardSign);
        }

        /// <summary>
        ///     get specified plugin, return null if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PluginPair GetPluginForId(string id) {
            return AllPlugins.FirstOrDefault(o => o.Metadata.ID == id);
        }

        public static IEnumerable<PluginPair> GetPluginsForInterface<T>() where T : IFeatures {
            return AllPlugins.Where(p => p.Plugin is T);
        }

        public static List<Result> GetContextMenusForPlugin(Result result) {
            PluginPair pluginPair = _contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (pluginPair != null) {
                PluginMetadata metadata = pluginPair.Metadata;
                IContextMenu plugin = (IContextMenu) pluginPair.Plugin;

                try {
                    List<Result> results = plugin.LoadContextMenus(result);
                    foreach (Result r in results) {
                        r.PluginDirectory = metadata.PluginDirectory;
                        r.PluginID = metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }

                    return results;
                } catch (Exception e) {
                    Log.Exception(
                        $"|PluginManager.GetContextMenusForPlugin|Can't load context menus for plugin <{metadata.Name}>",
                        e);
                    return new List<Result>();
                }
            }

            return new List<Result>();
        }

        public static bool ActionKeywordRegistered(string actionKeyword) {
            if (actionKeyword != Query.GlobalPluginWildcardSign &&
                NonGlobalPlugins.ContainsKey(actionKeyword)) {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     used to add action keyword for multiple action keyword plugin
        ///     e.g. web search
        /// </summary>
        public static void AddActionKeyword(string id, string newActionKeyword) {
            PluginPair plugin = GetPluginForId(id);
            if (newActionKeyword == Query.GlobalPluginWildcardSign) {
                GlobalPlugins.Add(plugin);
            } else {
                NonGlobalPlugins[newActionKeyword] = plugin;
            }

            plugin.Metadata.ActionKeywords.Add(newActionKeyword);
        }

        /// <summary>
        ///     used to add action keyword for multiple action keyword plugin
        ///     e.g. web search
        /// </summary>
        public static void RemoveActionKeyword(string id, string oldActionkeyword) {
            PluginPair plugin = GetPluginForId(id);
            if (oldActionkeyword == Query.GlobalPluginWildcardSign) {
                GlobalPlugins.Remove(plugin);
            } else {
                NonGlobalPlugins.Remove(oldActionkeyword);
            }

            plugin.Metadata.ActionKeywords.Remove(oldActionkeyword);
        }

        public static void ReplaceActionKeyword(string id, string oldActionKeyword, string newActionKeyword) {
            if (oldActionKeyword != newActionKeyword) {
                AddActionKeyword(id, newActionKeyword);
                RemoveActionKeyword(id, oldActionKeyword);
            }
        }
    }
}