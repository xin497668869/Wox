﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel {
    public class MainViewModel : BaseModel, ISavable {
        public void Query() {
            if (ResultsSelected()) {
                QueryResults();
            } else if (ContextMenuSelected()) {
                QueryContextMenu();
            } else if (HistorySelected()) {
                QueryHistory();
            }
        }

        private void QueryContextMenu() {
            const string id = "Context Menu ID";
            string query = QueryText.ToLower().Trim();
            ContextMenu.Clear();

            Result selected = Results.SelectedItem?.Result;

            if (selected != null) // SelectedItem returns null if selection is empty.
            {
                List<Result> results = PluginManager.GetContextMenusForPlugin(selected);
//                results.Add(ContextMenuTopMost(selected));
//                results.Add(ContextMenuPluginInfo(selected.PluginID));

                if (!string.IsNullOrEmpty(query)) {
                    List<Result> filtered = results.Where
                    (
                        r => StringMatcher.IsMatch(r.Title, query) ||
                             StringMatcher.IsMatch(r.SubTitle, query)
                    ).ToList();
                    ContextMenu.AddResults(filtered, id);
                } else {
                    ContextMenu.AddResults(results, id);
                }
            }
        }

        private void QueryHistory() {
            const string id = "Query History ID";
            string query = QueryText.ToLower().Trim();
            History.Clear();

            List<Result> results = new List<Result>();
            foreach (HistoryItem h in _history.Items) {
                string title = _translator.GetTranslation("executeQuery");
                string time = _translator.GetTranslation("lastExecuteTime");
                Result result = new Result {
                    Title = string.Format(title, h.Query),
                    SubTitle = string.Format(time, h.ExecutedDateTime),
                    IcoPath = "Images\\history.png",
                    OriginQuery = new Query {RawQuery = h.Query},
                    Action = _ => {
                        SelectedResults = Results;
                        ChangeQueryText(h.Query);
                        return false;
                    }
                };
                results.Add(result);
            }

            if (!string.IsNullOrEmpty(query)) {
                List<Result> filtered = results.Where
                (
                    r => StringMatcher.IsMatch(r.Title, query) ||
                         StringMatcher.IsMatch(r.SubTitle, query)
                ).ToList();
                History.AddResults(filtered, id);
            } else {
                History.AddResults(results, id);
            }
        }

        private void QueryResults() {
            if (!string.IsNullOrEmpty(QueryText)) {
                _updateSource?.Cancel();
                _updateSource = new CancellationTokenSource();
                _updateToken = _updateSource.Token;

                ProgressBarVisibility = Visibility.Hidden;
                _queryHasReturn = false;
                Query query = PluginManager.QueryInit(QueryText.Trim());
                if (query != null) {
                    // handle the exclusiveness of plugin using action keyword
                    string lastKeyword = _lastQuery.ActionKeyword;
                    string keyword = query.ActionKeyword;
                    if (string.IsNullOrEmpty(lastKeyword)) {
                        if (!string.IsNullOrEmpty(keyword)) {
                            Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                        }
                    } else {
                        if (string.IsNullOrEmpty(keyword)) {
                            Results.RemoveResultsFor(PluginManager.NonGlobalPlugins[lastKeyword].Metadata);
                        } else if (lastKeyword != keyword) {
                            Results.RemoveResultsExcept(PluginManager.NonGlobalPlugins[keyword].Metadata);
                        }
                    }

                    _lastQuery = query;
                    Task.Delay(200, _updateToken).ContinueWith(_ => {
                        if (query.RawQuery == _lastQuery.RawQuery && !_queryHasReturn) {
                            ProgressBarVisibility = Visibility.Visible;
                        }
                    }, _updateToken);

                    List<PluginPair> plugins = PluginManager.ValidPluginsForQuery(query);
                    Task.Run(() => {
                        Parallel.ForEach(plugins, plugin => {
                            Infrastructure.UserSettings.Plugin config =
                                _settings.PluginSettings.Plugins[plugin.Metadata.ID];
                            if (!config.Disabled) {
                                Dictionary<string, int> historyHistorySources = null;
                                if (_history.HistorySourcesMap.ContainsKey(plugin.Metadata.ID)) {
                                    historyHistorySources = _history.HistorySourcesMap[plugin.Metadata.ID];
                                } else {
                                    historyHistorySources = new Dictionary<string, int>();
                                    _history.HistorySourcesMap[plugin.Metadata.ID] = historyHistorySources;
                                }

                                List<Result> results =
                                    PluginManager.QueryForPlugin(plugin, query, historyHistorySources);
                                UpdateResultView(results, plugin.Metadata, query);
                            }
                        });
                    }, _updateToken);
                }
            } else {
                Results.Clear();
                Results.Visbility = Visibility.Collapsed;
            }
        }


        private Result ContextMenuTopMost(Result result) {
            Result menu;
            if (_topMostRecord.IsTopMost(result)) {
                menu = new Result {
                    Title = InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"),
                    IcoPath = "Images\\down.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ => {
                        _topMostRecord.Remove(result);
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
            } else {
                menu = new Result {
                    Title = InternationalizationManager.Instance.GetTranslation("setAsTopMostInThisQuery"),
                    IcoPath = "Images\\up.png",
                    PluginDirectory = Constant.ProgramDirectory,
                    Action = _ => {
                        _topMostRecord.AddOrUpdate(result);
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
            }

            return menu;
        }

        private Result ContextMenuPluginInfo(string id) {
            PluginMetadata metadata = PluginManager.GetPluginForId(id).Metadata;
            Internationalization translator = InternationalizationManager.Instance;

            string author = translator.GetTranslation("author");
            string website = translator.GetTranslation("website");
            string version = translator.GetTranslation("version");
            string plugin = translator.GetTranslation("plugin");
            string title = $"{plugin}: {metadata.Name}";
            string icon = metadata.IcoPath;
            string subtitle =
                $"{author}: {metadata.Author}, {website}: {metadata.Website} {version}: {metadata.Version}";

            Result menu = new Result {
                Title = title,
                IcoPath = icon,
                SubTitle = subtitle,
                PluginDirectory = metadata.PluginDirectory,
                Action = _ => false
            };
            return menu;
        }

        private bool ResultsSelected() {
            bool selected = SelectedResults == Results;
            return selected;
        }

        private bool ContextMenuSelected() {
            bool selected = SelectedResults == ContextMenu;
            return selected;
        }


        private bool HistorySelected() {
            bool selected = SelectedResults == History;
            return selected;
        }

        #region Private Fields

        private bool _queryHasReturn;
        private Query _lastQuery;
        private string _queryTextBeforeLeaveResults;

        private readonly WoxJsonStorage<History> _historyItemsStorage;
        private readonly WoxJsonStorage<UserSelectedRecord> _userSelectedRecordStorage;
        private readonly WoxJsonStorage<TopMostRecord> _topMostRecordStorage;
        private readonly Settings _settings;
        private readonly History _history;
        private readonly UserSelectedRecord _userSelectedRecord;
        private readonly TopMostRecord _topMostRecord;

        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;
        private bool _saved;

        private readonly Internationalization _translator = InternationalizationManager.Instance;

        #endregion

        #region Constructor

        public void addSearchHistory(string plugin, string msg) {
            _history.addSearchHistory(plugin, msg);
            _historyItemsStorage.Save();
        }

        public MainViewModel(Settings settings) {
            _saved = false;
            _queryTextBeforeLeaveResults = "";
            _queryText = "";
            _lastQuery = new Query();

            _settings = settings;

            _historyItemsStorage = new WoxJsonStorage<History>();
            _userSelectedRecordStorage = new WoxJsonStorage<UserSelectedRecord>();
            _topMostRecordStorage = new WoxJsonStorage<TopMostRecord>();
            _history = _historyItemsStorage.Load();
            if (_history.HistorySourcesMap == null) {
                _history.HistorySourcesMap = new Dictionary<string, Dictionary<string, int>>();
            }

            _userSelectedRecord = _userSelectedRecordStorage.Load();
            _topMostRecord = _topMostRecordStorage.Load();

            ContextMenu = new ResultsViewModel(_settings);
            Results = new ResultsViewModel(_settings);
            History = new ResultsViewModel(_settings);
            _selectedResults = Results;

            InitializeKeyCommands();
            RegisterResultsUpdatedEvent();

            SetHotkey(_settings.Hotkey, OnHotkey);
            SetCustomPluginHotkey();
        }

        private void RegisterResultsUpdatedEvent() {
            foreach (PluginPair pair in PluginManager.GetPluginsForInterface<IResultUpdated>()) {
                IResultUpdated plugin = (IResultUpdated) pair.Plugin;
                plugin.ResultsUpdated += (s, e) => {
                    Task.Run(() => {
                        PluginManager.UpdatePluginMetadata(e.Results, pair.Metadata, e.Query);
                        UpdateResultView(e.Results, pair.Metadata, e.Query);
                    }, _updateToken);
                };
            }
        }


        private void InitializeKeyCommands() {
            EscCommand = new RelayCommand(_ => {
                if (!ResultsSelected()) {
                    SelectedResults = Results;
                    MainWindowVisibility = Visibility.Collapsed;
                } else {
                    MainWindowVisibility = Visibility.Collapsed;
                }
            });

            SelectNextItemCommand = new RelayCommand(_ => { SelectedResults.SelectNextResult(); });

            SelectPrevItemCommand = new RelayCommand(_ => { SelectedResults.SelectPrevResult(); });

            SelectNextPageCommand = new RelayCommand(_ => { SelectedResults.SelectNextPage(); });

            SelectPrevPageCommand = new RelayCommand(_ => { SelectedResults.SelectPrevPage(); });

            StartHelpCommand = new RelayCommand(_ => { Process.Start("http://doc.wox.one/"); });

            OpenResultCommand = new RelayCommand(index => {
                ResultsViewModel results = SelectedResults;

                if (index != null) {
                    results.SelectedIndex = int.Parse(index.ToString());
                }

                Result result = results.SelectedItem?.Result;
                if (result != null) // SelectedItem returns null if selection is empty.
                {
                    bool hideWindow = result.Action != null && result.Action(new ActionContext {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (hideWindow) {
                        MainWindowVisibility = Visibility.Collapsed;
                    }

                    if (ResultsSelected()) {
                        _userSelectedRecord.Add(result);
                        _history.Add(result.OriginQuery.RawQuery);
                        if (result.historySave != null) {
                            addSearchHistory(result.PluginID, result.historySave);
                        }
                    }
                }
            });

            LoadContextMenuCommand = new RelayCommand(_ => {
                if (ResultsSelected()) {
                    SelectedResults = ContextMenu;
                } else {
                    SelectedResults = Results;
                }
            });

            LoadHistoryCommand = new RelayCommand(_ => {
                if (ResultsSelected()) {
                    SelectedResults = History;
                    History.SelectedIndex = _history.Items.Count - 1;
                } else {
                    SelectedResults = Results;
                }
            });
        }

        #endregion

        #region ViewModel Properties

        public ResultsViewModel Results { get; }
        public ResultsViewModel ContextMenu { get; }
        public ResultsViewModel History { get; }

        private string _queryText;

        public string QueryText {
            get => _queryText;
            set {
                _queryText = value;
                Query();
            }
        }

        /// <summary>
        ///     we need move cursor to end when we manually changed query
        ///     but we don't want to move cursor to end when query is updated from TextBox
        /// </summary>
        /// <param name="queryText"></param>
        public void ChangeQueryText(string queryText) {
            QueryTextCursorMovedToEnd = true;
            QueryText = queryText;
        }

        public bool LastQuerySelected { get; set; }
        public bool QueryTextCursorMovedToEnd { get; set; }

        private ResultsViewModel _selectedResults;

        private ResultsViewModel SelectedResults {
            get => _selectedResults;
            set {
                _selectedResults = value;
                if (ResultsSelected()) {
                    ContextMenu.Visbility = Visibility.Collapsed;
                    History.Visbility = Visibility.Collapsed;
                    ChangeQueryText(_queryTextBeforeLeaveResults);
                } else {
                    Results.Visbility = Visibility.Collapsed;
                    _queryTextBeforeLeaveResults = QueryText;


                    // Because of Fody's optimization
                    // setter won't be called when property value is not changed.
                    // so we need manually call Query()
                    // http://stackoverflow.com/posts/25895769/revisions
                    if (string.IsNullOrEmpty(QueryText)) {
                        Query();
                    } else {
                        QueryText = string.Empty;
                    }
                }

                _selectedResults.Visbility = Visibility.Visible;
            }
        }

        public Visibility ProgressBarVisibility { get; set; }

        public Visibility MainWindowVisibility { get; set; }

        public ICommand EscCommand { get; set; }
        public ICommand SelectNextItemCommand { get; set; }
        public ICommand SelectPrevItemCommand { get; set; }
        public ICommand SelectNextPageCommand { get; set; }
        public ICommand SelectPrevPageCommand { get; set; }
        public ICommand StartHelpCommand { get; set; }
        public ICommand LoadContextMenuCommand { get; set; }
        public ICommand LoadHistoryCommand { get; set; }
        public ICommand OpenResultCommand { get; set; }
        public ICommand EnterContextItem { get; set; }

        #endregion

        #region Hotkey

        private void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action) {
            HotkeyModel hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        private void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action) {
            string hotkeyStr = hotkey.ToString();
            try {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            } catch (Exception) {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"),
                        hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        public void RemoveHotkey(string hotkeyStr) {
            if (!string.IsNullOrEmpty(hotkeyStr)) {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        /// <summary>
        ///     Checks if Wox should ignore any hotkeys
        /// </summary>
        /// <returns></returns>
        private bool ShouldIgnoreHotkeys() {
            //double if to omit calling win32 function
            if (_settings.IgnoreHotkeysOnFullscreen) {
                if (WindowsInteropHelper.IsWindowFullscreen()) {
                    return true;
                }
            }

            return false;
        }

        private void SetCustomPluginHotkey() {
            if (_settings.CustomPluginHotkeys == null) {
                return;
            }

            foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys) {
                SetHotkey(hotkey.Hotkey, (s, e) => {
                    if (ShouldIgnoreHotkeys()) {
                        return;
                    }

                    MainWindowVisibility = Visibility.Visible;
                    ChangeQueryText(hotkey.ActionKeyword);
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e) {
            if (!ShouldIgnoreHotkeys()) {
                if (_settings.LastQueryMode == LastQueryMode.Empty) {
                    ChangeQueryText(string.Empty);
                } else if (_settings.LastQueryMode == LastQueryMode.Preserved) {
                    LastQuerySelected = true;
                } else if (_settings.LastQueryMode == LastQueryMode.Selected) {
                    LastQuerySelected = false;
                } else {
                    throw new ArgumentException($"wrong LastQueryMode: <{_settings.LastQueryMode}>");
                }

                ToggleWox();
                e.Handled = true;
            }
        }

        private void ToggleWox() {
            if (MainWindowVisibility != Visibility.Visible) {
                MainWindowVisibility = Visibility.Visible;
            } else {
                MainWindowVisibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Public Methods

        public void Save() {
            if (!_saved) {
                _historyItemsStorage.Save();
                _userSelectedRecordStorage.Save();
                _topMostRecordStorage.Save();

                _saved = true;
            }
        }

        /// <summary>
        ///     To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void UpdateResultView(List<Result> list, PluginMetadata metadata, Query originQuery) {
            _queryHasReturn = true;
            ProgressBarVisibility = Visibility.Hidden;

            foreach (Result result in list) {
                if (_topMostRecord.IsTopMost(result)) {
                    result.Score = int.MaxValue;
                } else {
                    result.Score += _userSelectedRecord.GetSelectedCount(result) * 5;
                }
            }

            if (originQuery.RawQuery == _lastQuery.RawQuery) {
                Results.AddResults(list, metadata.ID);
            }

            if (Results.Visbility != Visibility.Visible && list.Count > 0) {
                Results.Visbility = Visibility.Visible;
            }
        }

        #endregion
    }
}