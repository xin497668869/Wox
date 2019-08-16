using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace Wox.Plugin {
    public class Result {
        public delegate ImageSource IconDelegate();

        private string _icoPath;

        private string _pluginDirectory;

        /**
         * 需要作为历史记录存储的值
         */
        public string historySave;

        public IconDelegate Icon;

        [Obsolete("Use Object initializers instead")]
        public Result(string Title, string IcoPath, string SubTitle = null) {
            this.Title = Title;
            this.IcoPath = IcoPath;
            this.SubTitle = SubTitle;
        }

        public Result() {
        }

        public string Title { get; set; }
        public string SubTitle { get; set; }

        public string IcoPath {
            get => _icoPath;
            set {
                if (!string.IsNullOrEmpty(PluginDirectory) && !Path.IsPathRooted(value)) {
                    _icoPath = Path.Combine(value, IcoPath);
                } else {
                    _icoPath = value;
                }
            }
        }


        /// <summary>
        ///     return true to hide wox after select result
        /// </summary>
        public Func<ActionContext, bool> Action { get; set; }

        public int Score { get; set; }

        /// <summary>
        ///     Only resulsts that originQuery match with curren query will be displayed in the panel
        /// </summary>
        internal Query OriginQuery { get; set; }

        /// <summary>
        ///     Plugin directory
        /// </summary>
        public string PluginDirectory {
            get => _pluginDirectory;
            set {
                _pluginDirectory = value;
                if (!string.IsNullOrEmpty(IcoPath) && !Path.IsPathRooted(IcoPath)) {
                    IcoPath = Path.Combine(value, IcoPath);
                }
            }
        }

        [Obsolete("Use IContextMenu instead")]
        /// <summary>
        /// Context menus associate with this result
        /// </summary>
        public List<Result> ContextMenu { get; set; }

        /// <summary>
        ///     Additional data associate with this result
        /// </summary>
        public object ContextData { get; set; }

        /// <summary>
        ///     Plugin ID that generate this result
        /// </summary>
        public string PluginID { get; set; }

        public override bool Equals(object obj) {
            Result r = obj as Result;
            if (r != null) {
                bool equality = string.Equals(r.Title, Title) &&
                                string.Equals(r.SubTitle, SubTitle);
                return equality;
            }

            return false;
        }

        public override int GetHashCode() {
            int hashcode = (Title?.GetHashCode() ?? 0) ^
                           (SubTitle?.GetHashCode() ?? 0);
            return hashcode;
        }

        public override string ToString() {
            return Title + SubTitle;
        }
    }
}