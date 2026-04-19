using GitBashDesktop.Models;
using GitBashDesktop.Services;
using GitBashDesktop.ViewModels;
using System.Windows.Controls;

namespace GitBashDesktop.Views
{
    public partial class MergeConflictsView : UserControl
    {
        public MergeConflictsView(GitService git)
        {
            InitializeComponent();
            DataContext = new MergeConflictsViewModel(git);
        }

        private void ConflictFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView lv &&
                lv.SelectedItem is ConflictFile file &&
                DataContext is MergeConflictsViewModel vm)
            {
                vm.SelectFileCommand.Execute(file);
            }
        }
    }
}