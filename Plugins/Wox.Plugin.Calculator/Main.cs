using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using Mages.Core;

namespace Wox.Plugin.Caculator {
    public class Main : IPlugin, IPluginI18n {
        private static readonly Regex RegValidExpressChar = new Regex(
            @"^(" +
            @"ceil|floor|exp|pi|e|max|min|det|abs|log|ln|sqrt|" +
            @"sin|cos|tan|arcsin|arccos|arctan|" +
            @"eigval|eigvec|eig|sum|polar|plot|round|sort|real|zeta|" +
            @"bin2dec|hex2dec|oct2dec|" +
            @"==|~=|&&|\|\||" +
            @"[ei]|[0-9]|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
            @")+$", RegexOptions.Compiled);

        private static readonly Regex RegBrackets = new Regex(@"[\(\)\[\]]", RegexOptions.Compiled);
        private static readonly Engine MagesEngine;

        static Main() {
            MagesEngine = new Engine();
        }

        private PluginInitContext Context { get; set; }

        public List<Result> Query(Query query, Dictionary<string, int> historyHistorySources) {
            if (query.Search.Length <= 2 // don't affect when user only input "e" or "i" keyword
                || !RegValidExpressChar.IsMatch(query.Search)
                || !IsBracketComplete(query.Search)) {
                return new List<Result>();
            }

            try {
                object result = MagesEngine.Interpret(query.Search);

                if (result.ToString() == "NaN") {
                    result = Context.API.GetTranslation("wox_plugin_calculator_not_a_number");
                }

                if (result is Function) {
                    result = Context.API.GetTranslation("wox_plugin_calculator_expression_not_complete");
                }


                if (!string.IsNullOrEmpty(result?.ToString())) {
                    return new List<Result> {
                        new Result {
                            Title = result.ToString(),
                            IcoPath = "Images/calculator.png",
                            Score = 300,
                            SubTitle = Context.API.GetTranslation("wox_plugin_calculator_copy_number_to_clipboard"),
                            Action = c => {
                                try {
                                    Clipboard.SetText(result.ToString());
                                    return true;
                                } catch (ExternalException) {
                                    MessageBox.Show("Copy failed, please try later");
                                    return false;
                                }
                            }
                        }
                    };
                }
            } catch {
                // ignored
            }

            return new List<Result>();
        }

        public void Init(PluginInitContext context) {
            Context = context;
        }

        public string GetTranslatedPluginTitle() {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_name");
        }

        public string GetTranslatedPluginDescription() {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_description");
        }

        private bool IsBracketComplete(string query) {
            MatchCollection matchs = RegBrackets.Matches(query);
            int leftBracketCount = 0;
            foreach (Match match in matchs) {
                if (match.Value == "(" || match.Value == "[") {
                    leftBracketCount++;
                } else {
                    leftBracketCount--;
                }
            }

            return leftBracketCount == 0;
        }
    }
}