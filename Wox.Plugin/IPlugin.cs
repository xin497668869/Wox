using System.Collections.Generic;

namespace Wox.Plugin {
    public interface IPlugin {
        List<Result> Query(Query query, Dictionary<string, int> historyHistorySources);
        void Init(PluginInitContext context);
    }
}