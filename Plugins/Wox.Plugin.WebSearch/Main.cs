using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch {
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable, IResultUpdated {
        public const string Images = "Images";
        public static string ImagesDirectory;

        private readonly Settings _settings;
        private readonly SettingsViewModel _viewModel;
        private PluginInitContext _context;
        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;

        public Main() {
            _viewModel = new SettingsViewModel();
            _settings = _viewModel.Settings;
        }

        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            _updateSource?.Cancel();
            _updateSource = new CancellationTokenSource();
            _updateToken = _updateSource.Token;

            SearchSource searchSource =
                _settings.SearchSources.FirstOrDefault(o => o.ActionKeyword == query.ActionKeyword && o.Enabled);

            if (searchSource != null) {
                string keyword = query.Search;
                string title = keyword;
                string subtitle = _context.API.GetTranslation("wox_plugin_websearch_search") + " " + searchSource.Title;
                if (string.IsNullOrEmpty(keyword)) {
                    Result result = new Result {
                        Title = subtitle,
                        SubTitle = string.Empty,
                        IcoPath = searchSource.IconPath
                    };
                    return new List<Result> {result};
                } else {
                    List<Result> results = new List<Result>();
                    Result result = new Result {
                        Title = title,
                        SubTitle = subtitle,
                        Score = 6,
                        IcoPath = searchSource.IconPath,
                        Action = c => {
                            Process.Start(searchSource.Url.Replace("{q}", Uri.EscapeDataString(keyword)));
                            return true;
                        }
                    };
                    results.Add(result);
                    UpdateResultsFromSuggestion(results, keyword, subtitle, searchSource, query);
                    return results;
                }
            }

            return new List<Result>();
        }

        public void Init(PluginInitContext context) {
            _context = context;
            string pluginDirectory = _context.CurrentPluginMetadata.PluginDirectory;
            string bundledImagesDirectory = Path.Combine(pluginDirectory, Images);
            ImagesDirectory = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, Images);
            Helper.ValidateDataDirectory(bundledImagesDirectory, ImagesDirectory);
        }

        public string GetTranslatedPluginTitle() {
            return _context.API.GetTranslation("wox_plugin_websearch_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return _context.API.GetTranslation("wox_plugin_websearch_plugin_description");
        }

        public event ResultUpdatedEventHandler ResultsUpdated;

        public void Save() {
            _viewModel.Save();
        }

        #region ISettingProvider Members

        public Control CreateSettingPanel() {
            return new SettingsControl(_context, _viewModel);
        }

        #endregion

        private void UpdateResultsFromSuggestion(List<Result> results, string keyword, string subtitle,
            SearchSource searchSource, Query query) {
            if (_settings.EnableSuggestion) {
                const int waittime = 300;
                Task task = Task.Run(async () => {
                    IEnumerable<Result> suggestions = await Suggestions(keyword, subtitle, searchSource);
                    results.AddRange(suggestions);
                }, _updateToken);

                if (!task.Wait(waittime)) {
                    task.ContinueWith(_ => ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs {
                        Results = results,
                        Query = query
                    }), _updateToken);
                }
            }
        }

        private async Task<IEnumerable<Result>>
            Suggestions(string keyword, string subtitle, SearchSource searchSource) {
            SuggestionSource source = _settings.SelectedSuggestion;
            if (source != null) {
                List<string> suggestions = await source.Suggestions(keyword);
                IEnumerable<Result> resultsFromSuggestion = suggestions.Select(o => new Result {
                    Title = o,
                    SubTitle = subtitle,
                    Score = 5,
                    IcoPath = searchSource.IconPath,
                    Action = c => {
                        Process.Start(searchSource.Url.Replace("{q}", Uri.EscapeDataString(o)));
                        return true;
                    }
                });
                return resultsFromSuggestion;
            }

            return new List<Result>();
        }
    }
}