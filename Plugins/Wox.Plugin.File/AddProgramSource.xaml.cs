using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace Wox.Plugin.Program {
    /// <summary>
    ///     Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource {
        private readonly PluginInitContext _context;
        private readonly Settings.ProgramSource _editing;

        private readonly Settings _settings;
//        private TextBox Directory;
//        private TextBox PriorityTextBox;

        public AddProgramSource(PluginInitContext context, Settings settings) {
            InitializeComponent();
            _context = context;
            _settings = settings;
            DirectoryTextBox.Focus();
        }

        public AddProgramSource(Settings.ProgramSource edit, Settings settings) {
            _editing = edit;
            _settings = settings;

            InitializeComponent();
            DirectoryTextBox.Text = _editing.Location;
            PriorityTextBox.Text = _editing.Priority + "";
            DeepTextBox.Text = _editing.Deep + "";
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK) {
                DirectoryTextBox.Text = dialog.SelectedPath;
            }
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e) {
            string s = DirectoryTextBox.Text;
            if (!Directory.Exists(s)) {
                MessageBox.Show(_context.API.GetTranslation("wox_plugin_program_invalid_path"));
                return;
            }

            if (_editing == null) {
                Settings.ProgramSource source = new Settings.ProgramSource {
                    Location = DirectoryTextBox.Text,
                    Priority = Convert.ToInt32(PriorityTextBox.Text),
                    Deep = Convert.ToInt32(DeepTextBox.Text)
                };
                _settings.ProgramSources.Add(source);
            } else {
                _editing.Location = DirectoryTextBox.Text;
                _editing.Priority = Convert.ToInt32(PriorityTextBox.Text);
                _editing.Deep = Convert.ToInt32(DeepTextBox.Text);
            }

            DialogResult = true;
            Close();
        }
    }
}