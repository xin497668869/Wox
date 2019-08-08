using System.Collections.Generic;

namespace Wox.Plugin.Program {
    public class Settings {
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public Dictionary<string, int> HistorySourcesMap { get; set; } = new Dictionary<string, int>();

        public class ProgramSource {
            public ProgramSource() {
            }

            public ProgramSource(string location, int priority, int deep) {
                Location = location;
                Priority = priority;
                Deep = deep;
            }

            public string Location { get; set; }

            public int Priority { get; set; }

            public int Deep { get; set; }
        }


//        public class HistorySource
//        {
//            public HistorySource()
//            {
//            }
//
//            public HistorySource(string location, string date)
//            {
//                Location = location;
//                Date = date;
//            }
//
//            public string Location { get; set; }
//
//            public int Date { get; set; }
//        }
    }
}