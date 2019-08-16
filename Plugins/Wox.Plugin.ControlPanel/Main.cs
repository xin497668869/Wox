using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure;

namespace Wox.Plugin.ControlPanel {
    public class Main : IPlugin, IPluginI18n {
        private PluginInitContext context;
        private List<ControlPanelItem> controlPanelItems = new List<ControlPanelItem>();
        private string fileType;
        private string iconFolder;

        public void Init(PluginInitContext context) {
            this.context = context;
            controlPanelItems = ControlPanelList.Create(48);
            iconFolder = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, @"Images\ControlPanelIcons\");
            fileType = ".bmp";

            if (!Directory.Exists(iconFolder)) {
                Directory.CreateDirectory(iconFolder);
            }


            foreach (ControlPanelItem item in controlPanelItems) {
                if (!File.Exists(iconFolder + item.GUID + fileType) && item.Icon != null) {
                    item.Icon.ToBitmap().Save(iconFolder + item.GUID + fileType);
                }
            }
        }

        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            List<Result> results = new List<Result>();

            foreach (ControlPanelItem item in controlPanelItems) {
                item.Score = Score(item, query.Search);
                if (item.Score > 0) {
                    Result result = new Result {
                        Title = item.LocalizedString,
                        SubTitle = item.InfoTip,
                        Score = item.Score,
                        IcoPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory,
                            @"Images\\ControlPanelIcons\\" + item.GUID + fileType),
                        Action = e => {
                            try {
                                Process.Start(item.ExecutablePath);
                            } catch (Exception) {
                                //Silently Fail for now.. todo
                            }

                            return true;
                        }
                    };
                    results.Add(result);
                }
            }

            List<Result> panelItems = results.OrderByDescending(o => o.Score).Take(5).ToList();
            return panelItems;
        }

        public string GetTranslatedPluginTitle() {
            return context.API.GetTranslation("wox_plugin_controlpanel_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return context.API.GetTranslation("wox_plugin_controlpanel_plugin_description");
        }

        private int Score(ControlPanelItem item, string query) {
            List<int> scores = new List<int> {0};
            if (string.IsNullOrEmpty(item.LocalizedString)) {
                int score1 = StringMatcher.Score(item.LocalizedString, query);
                int score2 = StringMatcher.ScoreForPinyin(item.LocalizedString, query);
                scores.Add(score1);
                scores.Add(score2);
            }

            if (!string.IsNullOrEmpty(item.InfoTip)) {
                int score1 = StringMatcher.Score(item.InfoTip, query);
                int score2 = StringMatcher.ScoreForPinyin(item.InfoTip, query);
                scores.Add(score1);
                scores.Add(score2);
            }

            return scores.Max();
        }
    }
}