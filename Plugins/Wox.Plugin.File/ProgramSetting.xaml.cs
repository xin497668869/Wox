using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.Program {
    /// <summary>
    ///     Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl {
        private readonly Settings _settings;
        private readonly PluginInitContext context;

        public ProgramSetting(PluginInitContext context, Settings settings) {
            this.context = context;
            InitializeComponent();
            Loaded += Setting_Loaded;
            _settings = settings;
        }

        private void ReIndexing() {
            programSourceView.Items.Refresh();
//            programHistoryView.Items.Refresh();
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e) {
            programSourceView.ItemsSource = _settings.ProgramSources;
        }


        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e) {
            AddProgramSource add = new AddProgramSource(context, _settings);
            add.ShowDialog();
            ReIndexing();
        }

        private void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e) {
            Settings.ProgramSource selectedProgramSource = programSourceView.SelectedItem as Settings.ProgramSource;
            if (selectedProgramSource != null) {
                string msg = string.Format(context.API.GetTranslation("wox_plugin_program_delete_program_source"),
                    selectedProgramSource.Location);

                if (MessageBox.Show(msg, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    _settings.ProgramSources.Remove(selectedProgramSource);
                    ReIndexing();
                }
            } else {
                string msg = context.API.GetTranslation("wox_plugin_program_pls_select_program_source");
                MessageBox.Show(msg);
            }
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e) {
            Settings.ProgramSource selectedProgramSource = programSourceView.SelectedItem as Settings.ProgramSource;
            if (selectedProgramSource != null) {
                AddProgramSource add = new AddProgramSource(selectedProgramSource, _settings);
                if (add.ShowDialog() ?? false) {
                    ReIndexing();
                }
            } else {
                string msg = context.API.GetTranslation("wox_plugin_program_pls_select_program_source");
                MessageBox.Show(msg);
            }
        }


        private void programSourceView_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Link;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void programSourceView_Drop(object sender, DragEventArgs e) {
            string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0) {
                foreach (string s in files) {
                    if (Directory.Exists(s)) {
                        _settings.ProgramSources.Add(new Settings.ProgramSource {
                            Location = s
                        });
                    }
                }
            }
        }

//        private void cleanHistoryClick(object sender, RoutedEventArgs e) {
//            _settings.HistorySourcesMap.Clear();
//        }
    }
}