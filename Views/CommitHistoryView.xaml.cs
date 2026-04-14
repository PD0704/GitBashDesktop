using GitBashDesktop.Services;
using GitBashDesktop.ViewModels;
using System.Windows.Controls;

namespace GitBashDesktop.Views
{
    public partial class CommitHistoryView : UserControl
    {
        public CommitHistoryView(GitService git)
        {
            InitializeComponent();
            DataContext = new CommitHistoryViewModel(git);
        }
    }
}