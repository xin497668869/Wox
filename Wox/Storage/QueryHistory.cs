using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Storage {
    public class History {
        private readonly int _maxHistory = 300;
        public List<HistoryItem> Items { get; set; } = new List<HistoryItem>();

        public Dictionary<string, Dictionary<string, int>> HistorySourcesMap { get; set; }

        public void addSearchHistory(string plugin, string msg) {
            if (HistorySourcesMap == null) {
                HistorySourcesMap = new Dictionary<string, Dictionary<string, int>>();
            }

            Dictionary<string, int> pluginHistorySources;
            if (!HistorySourcesMap.TryGetValue(plugin, out pluginHistorySources)) {
                pluginHistorySources = new Dictionary<string, int>();
                HistorySourcesMap[plugin] = pluginHistorySources;
            }

            pluginHistorySources.Remove(msg);
            pluginHistorySources.Add(msg, (int) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (pluginHistorySources.Count > 100) {
                int minTime = int.MaxValue;
                string fileName = null;

                foreach (KeyValuePair<string, int> keyValuePair in pluginHistorySources) {
                    if (keyValuePair.Value < minTime) {
                        minTime = keyValuePair.Value;
                        fileName = keyValuePair.Key;
                    }
                }

                if (fileName != null) {
                    pluginHistorySources.Remove(fileName);
                }
            }
        }

        public void Add(string query) {
            if (string.IsNullOrEmpty(query)) {
                return;
            }

            if (Items.Count > _maxHistory) {
                Items.RemoveAt(0);
            }

            if (Items.Count > 0 && Items.Last().Query == query) {
                Items.Last().ExecutedDateTime = DateTime.Now;
            } else {
                Items.Add(new HistoryItem {
                    Query = query,
                    ExecutedDateTime = DateTime.Now
                });
            }
        }
    }
}