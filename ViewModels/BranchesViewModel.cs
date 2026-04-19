using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;


namespace GitBashDesktop.ViewModels
{
    public partial class BranchesViewModel : ObservableObject
    {
        private readonly GitService _git;
        private string _defaultBranch = "main";
        [ObservableProperty] private string _currentBranch = "";
        [ObservableProperty] private string _newBranchName = "";
        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private BranchInfo? _selectedBranch;

        public ObservableCollection<BranchInfo> LocalBranches { get; } = new();
        public ObservableCollection<BranchInfo> RemoteBranches { get; } = new();

        public BranchesViewModel(GitService git)
        {
            _git = git;
            _ = LoadBranchesAsync();
        }

        // ── Load all branches ─────────────────────────────────────────────
        [RelayCommand]
        private async Task LoadBranchesAsync()
        {
            if (!_git.HasRepo) return;

            var defaultResult = await _git.GetDefaultBranchAsync();
            _defaultBranch = defaultResult;

            IsBusy = true;
            LocalBranches.Clear();
            RemoteBranches.Clear();

            var currentResult = await _git.GetCurrentBranchAsync();
            CurrentBranch = currentResult.Success ? currentResult.Output.Trim() : "";

            var result = await _git.GetBranchesAsync();
            if (!result.Success) { IsBusy = false; return; }

            foreach (var line in result.Output.Split('\n'))
            {
                var trimmed = line.Trim().TrimStart('*').Trim();

                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.Contains("->")) continue;

                if (trimmed.StartsWith("remotes/"))
                {
                    var name = trimmed.Substring("remotes/".Length);
                    var branchName = name.Contains("/")
                        ? name.Substring(name.LastIndexOf('/') + 1)
                        : name;
                    RemoteBranches.Add(new BranchInfo(
                        name,
                        isLocal: false,
                        isCurrent: false,
                        isDefault: branchName == _defaultBranch));
                }
                else
                {
                    LocalBranches.Add(new BranchInfo(
                        trimmed,
                        isLocal: true,
                        isCurrent: trimmed == CurrentBranch,
                        isDefault: trimmed == _defaultBranch));
                }
            }

            Views.MainWindow.UpdateCommandBar(
                "git branch -a",
                "lists all local and remote branches");

            IsBusy = false;
        }

        // ── Checkout Remote ─────────────────────────────────────────────
        [RelayCommand]
        private async Task CheckoutRemoteAsync(BranchInfo branch)
        {
            // branch.Name is like "origin/TestBranch"
            // We need just "TestBranch" as the local name
            var localName = branch.Name.Contains("/")
                ? branch.Name.Substring(branch.Name.LastIndexOf('/') + 1)
                : branch.Name;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git checkout -b {localName} {branch.Name}",
                "creates a local branch tracking the remote branch");

            var result = await _git.RunAsync($"checkout -b {localName} {branch.Name}");

            if (result.Success)
            {
                await LoadBranchesAsync();
                MessageBox.Show($"Checked out '{localName}' from '{branch.Name}'.",
                    "Checkout successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Branch might already exist locally, just switch to it
                var switchResult = await _git.SwitchBranchAsync(localName);
                if (result.Success)
                {
                    await LoadBranchesAsync();
                    Views.MainWindow.Instance?.ReinitBranchesView();

                    // Also refresh dashboard
                    var dashVm = (Views.MainWindow.Instance?._dashboardView?.DataContext
                        as DashboardViewModel);
                    _ = dashVm?.RefreshCommand.ExecuteAsync(null);
                }
                else
                    MessageBox.Show(
                        "Could not checkout branch. Check the terminal for details.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            IsBusy = false;
        }

        // ── Delete Remote ─────────────────────────────────────────────
        [RelayCommand]
        private async Task DeleteRemoteAsync(BranchInfo branch)
        {
            // branch.Name is like "origin/TestBranch"
            var parts = branch.Name.Split('/');
            if (parts.Length < 2) return;

            var remote = parts[0];
            var branchName = string.Join("/", parts[1..]);

            if (branchName == _defaultBranch)
            {
                MessageBox.Show($"'{branchName}' is the default branch and cannot be deleted from remote.",
                    "Protected branch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete remote branch '{branch.Name}'?\nThis cannot be undone.",
                "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git push {remote} --delete {branchName}",
                "deletes the branch from the remote repository");

            var result = await _git.RunAsync($"push {remote} --delete {branchName}");

            if (result.Success)
                await LoadBranchesAsync();
            else
                MessageBox.Show("Could not delete remote branch. Check the terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Switch branch ─────────────────────────────────────────────────
        [RelayCommand]
        private async Task SwitchBranchAsync(BranchInfo branch)
        {
            if (branch.Name == CurrentBranch) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git checkout {branch.Name}",
                "switches to the selected branch");

            var result = await _git.SwitchBranchAsync(branch.Name);
            if (result.Success)
                await LoadBranchesAsync();
            else
                MessageBox.Show(
                    "Could not switch branch. You may have uncommitted changes.",
                    "Switch failed", MessageBoxButton.OK, MessageBoxImage.Warning);

            IsBusy = false;
        }

        // ── Create branch ─────────────────────────────────────────────────
        [RelayCommand]
        private async Task CreateBranchAsync()
        {
            if (string.IsNullOrWhiteSpace(NewBranchName))
            {
                MessageBox.Show("Please enter a branch name.",
                    "Empty name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git checkout -b {NewBranchName}",
                "creates and switches to a new branch");

            var result = await _git.CreateBranchAsync(NewBranchName);
            if (result.Success)
            {
                NewBranchName = "";
                await LoadBranchesAsync();
            }
            else
                MessageBox.Show("Could not create branch. Check the terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Delete branch ─────────────────────────────────────────────────
        [RelayCommand]
        private async Task DeleteBranchAsync(BranchInfo branch)
        {
            if (branch.Name == CurrentBranch)
            {
                MessageBox.Show("You cannot delete the branch you are currently on.",
                    "Cannot delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (branch.Name == _defaultBranch)
            {
                MessageBox.Show($"'{branch.Name}' is the default branch and cannot be deleted.",
                    "Protected branch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete branch '{branch.Name}'?",
                "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git branch -d {branch.Name}",
                "deletes the selected local branch");

            var result = await _git.DeleteBranchAsync(branch.Name);
            if (result.Success)
                await LoadBranchesAsync();
            else
                MessageBox.Show(
                    "Could not delete branch. It may have unmerged changes.\n" +
                    "Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Push ──────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task PushAsync()
        {
            if (!_git.HasRepo) return;
            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git push origin {CurrentBranch}",
                "uploads commits to the remote repository");

            await _git.PushAsync("origin", CurrentBranch);
            IsBusy = false;
        }

        // ── Pull ──────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task PullAsync()
        {
            if (!_git.HasRepo) return;
            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                "git pull",
                "downloads and merges remote changes");

            await _git.PullAsync();
            IsBusy = false;
        }

        // ── Fetch ─────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task FetchAsync()
        {
            if (!_git.HasRepo) return;
            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                "git fetch --all",
                "downloads all remote branch updates");

            await _git.FetchAsync();
            IsBusy = false;
        }

        // ── Merge ─────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task MergeAsync(BranchInfo branch)
        {
            if (branch.Name == CurrentBranch)
            {
                MessageBox.Show("You cannot merge a branch into itself.",
                    "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Merge '{branch.Name}' into '{CurrentBranch}'?",
                "Confirm merge", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git merge {branch.Name}",
                $"merges '{branch.Name}' into current branch");

            var result = await _git.MergeAsync(branch.Name);

            if (result.Success)
            {
                MessageBox.Show(
                    $"Merged '{branch.Name}' into '{CurrentBranch}' successfully!",
                    "Merge successful", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadBranchesAsync();
            }
            else if (result.Output.Contains("CONFLICT") ||
                     result.Error.Contains("CONFLICT"))
            {
                MessageBox.Show(
                    $"Merge conflict detected!\n\nGo to Merge Conflicts view to resolve them.",
                    "Conflicts found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("Merge failed. Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            IsBusy = false;
        }
    }

    // ── Branch model ──────────────────────────────────────────────────────
    public class BranchInfo : ObservableObject
    {
        public string Name { get; }
        public bool IsLocal { get; }
        public bool IsCurrent { get; }
        public bool IsDefault { get; }

        public string CurrentIndicator => IsCurrent ? "●" : "";
        public string NameColor => IsCurrent
            ? "#007ACC"
            : Application.Current.Resources["TextPrimaryBrush"] is SolidColorBrush brush
                ? brush.Color.ToString()
                : "#CCCCCC";
        public Visibility DeleteVisible => IsDefault
            ? Visibility.Collapsed
            : Visibility.Visible;

        public BranchInfo(string name, bool isLocal, bool isCurrent, bool isDefault = false)
        {
            Name = name;
            IsLocal = isLocal;
            IsCurrent = isCurrent;
            IsDefault = isDefault;
        }
    }
}