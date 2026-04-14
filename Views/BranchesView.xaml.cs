using GitBashDesktop.Services;
using GitBashDesktop.ViewModels;
using System.Windows.Controls;

namespace GitBashDesktop.Views
{
    public partial class BranchesView : UserControl
    {
        public BranchesView(GitService git)
        {
            InitializeComponent();
            DataContext = new BranchesViewModel(git);
        }
    }
}