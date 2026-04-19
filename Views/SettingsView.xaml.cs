using GitBashDesktop.Services;
using GitBashDesktop.ViewModels;
using System.Windows.Controls;

namespace GitBashDesktop.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView(GitService git, SettingsService settings)
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(git, settings);
        }
    }
}