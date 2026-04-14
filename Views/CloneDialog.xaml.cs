using System.Windows;

namespace GitBashDesktop.Views
{
    public partial class CloneDialog : Window
    {
        public string Url { get; private set; } = "";

        public CloneDialog() => InitializeComponent();

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            Url = UrlBox.Text.Trim();
            if (string.IsNullOrEmpty(Url)) return;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}