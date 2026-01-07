using System.Windows;

namespace Gui.Views
{
    public partial class RegexInputDialog : Window
    {
        public string RegexPattern { get; private set; } = string.Empty;

        public RegexInputDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            RegexPattern = RegexTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string pattern)
            {
                RegexTextBox.Text = pattern;
            }
        }
    }
}
