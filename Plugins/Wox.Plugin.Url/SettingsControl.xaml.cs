using System.Windows.Controls;

namespace Wox.Plugin.Url {
    /// <summary>
    ///     SettingsControl.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsControl : UserControl {
        private Settings _settings;
        private IPublicAPI _woxAPI;

        public SettingsControl(IPublicAPI woxAPI, Settings settings) {
            InitializeComponent();
            _settings = settings;
            _woxAPI = woxAPI;
        }
    }
}