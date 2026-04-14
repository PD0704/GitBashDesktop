using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Models;
using GitBashDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GitBashDesktop.ViewModels
{
    public partial class CommitHistoryViewModel : ObservableObject
    {
        private readonly GitService _git;

        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private CommitInfo? _selectedCommit = null;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private string _selectedBranch = "All branches";
        [ObservableProperty] private string _selectedFile = "";
        [ObservableProperty] private string _diffOutput = "";
        [ObservableProperty] private bool _showDiff = false;
        [ObservableProperty] private string _newBranchName = "";

        public ObservableCollection<CommitInfo> Commits { get; } = new();
        public ObservableCollection<CommitInfo> FilteredCommits { get; } = new();
        public ObservableCollection<string> Branches { get; } = new();
        public ObservableCollection<string> ChangedFiles { get; } = new();

        public CommitHistoryViewModel(GitService git)
        {
            _git = git;
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            await LoadBranchesAsync();
            await LoadCommitsAsync();
        }

        // ── Load branches for filter ──────────────────────────────────────────
        private async Task LoadBranchesAsync()
        {
            Branches.Clear();
            Branches.Add("All branches");

            var result = await _git.GetBranchNamesAsync();
            if (!result.Success) return;

            foreach (var line in result.Output.Split('\n'))
            {
                var b = line.Trim();
                if (!string.IsNullOrEmpty(b))
                    Branches.Add(b);
            }
        }

        // ── Load commits ──────────────────────────────────────────────────────
        [RelayCommand]
        private async Task LoadCommitsAsync()
        {
            if (!_git.HasRepo) return;

            IsBusy = true;
            Commits.Clear();
            FilteredCommits.Clear();
            SelectedCommit = null;
            ChangedFiles.Clear();
            ShowDiff = false;

            Views.MainWindow.UpdateCommandBar(
                "git log --pretty=format:\"%H|%an|%ae|%ad|%s\" --date=short",
                "shows detailed commit history");

            GitResult result;
            if (SelectedBranch == "All branches")
                result = await _git.GetLogDetailedAsync(100);
            else
                result = await _git.GetLogForBranchAsync(SelectedBranch, 100);

            if (!result.Success) { IsBusy = false; return; }

            foreach (var line in result.Output.Split('\n'))
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var commit = new CommitInfo
                {
                    Hash = parts[0].Trim(),
                    ShortHash = parts[0].Trim().Length >= 7
                        ? parts[0].Trim()[..7] : parts[0].Trim(),
                    Author = parts[1].Trim(),
                    Email = parts[2].Trim(),
                    Date = parts[3].Trim(),
                    Message = string.Join("|", parts[4..]).Trim()
                };

                Commits.Add(commit);
            }

            ApplyFilter();
            IsBusy = false;
        }

        // ── Filter ────────────────────────────────────────────────────────────
        partial void OnFilterTextChanged(string value) => ApplyFilter();
        partial void OnSelectedBranchChanged(string value) => _ = LoadCommitsAsync();

        private void ApplyFilter()
        {
            FilteredCommits.Clear();
            var search = FilterText.Trim();

            foreach (var c in Commits)
            {
                if (string.IsNullOrWhiteSpace(search) ||
                    c.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.Author.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.ShortHash.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredCommits.Add(c);
                }
            }
        }

        // ── Select commit ─────────────────────────────────────────────────────
        [RelayCommand]
        private async Task SelectCommitAsync(CommitInfo commit)
        {
            if (commit == null) return;
            SelectedCommit = commit;
            ShowDiff = false;
            IsBusy = true;
            ChangedFiles.Clear();

            Views.MainWindow.UpdateCommandBar(
                $"git show {commit.ShortHash} --name-only",
                "shows files changed in this commit");

            var filesResult = await _git.RunAsync(
                $"show {commit.Hash} --name-only --format=");

            if (filesResult.Success)
            {
                foreach (var line in filesResult.Output.Split('\n'))
                {
                    var f = line.Trim();
                    if (!string.IsNullOrEmpty(f))
                        ChangedFiles.Add(f);
                }
            }

            IsBusy = false;
        }

        // ── Show file diff ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task ShowFileDiffAsync(string filePath)
        {
            if (SelectedCommit == null || string.IsNullOrEmpty(filePath)) return;

            IsBusy = true;
            SelectedFile = filePath;
            ShowDiff = true;

            Views.MainWindow.UpdateCommandBar(
                $"git show {SelectedCommit.ShortHash} -- \"{filePath}\"",
                "shows exact changes made to this file");

            var result = await _git.GetFileDiffAsync(SelectedCommit.Hash, filePath);
            DiffOutput = result.Success ? result.Output : "Could not load diff.";

            IsBusy = false;
        }

        // ── Copy hash ─────────────────────────────────────────────────────────
        [RelayCommand]
        private void CopyShortHash()
        {
            if (SelectedCommit == null) return;
            Clipboard.SetText(SelectedCommit.ShortHash);
            MessageBox.Show($"Copied: {SelectedCommit.ShortHash}",
                "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void CopyFullHash()
        {
            if (SelectedCommit == null) return;
            Clipboard.SetText(SelectedCommit.Hash);
            MessageBox.Show($"Copied: {SelectedCommit.Hash}",
                "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Revert commit ─────────────────────────────────────────────────────
        [RelayCommand]
        private async Task RevertCommitAsync()
        {
            if (SelectedCommit == null) return;

            var confirm = MessageBox.Show(
                $"Revert commit:\n\"{SelectedCommit.Message}\"\n\n" +
                "This will create a new commit that undoes these changes.",
                "Revert commit", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git revert {SelectedCommit.ShortHash} --no-edit",
                "creates a new commit that undoes the selected commit");

            var result = await _git.RevertCommitAsync(SelectedCommit.Hash);

            if (result.Success)
            {
                MessageBox.Show("Commit reverted successfully!",
                    "Reverted", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadCommitsAsync();
            }
            else
                MessageBox.Show("Revert failed. Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Create branch from commit ─────────────────────────────────────────
        [RelayCommand]
        private async Task CreateBranchFromCommitAsync()
        {
            if (SelectedCommit == null) return;

            if (string.IsNullOrWhiteSpace(NewBranchName))
            {
                MessageBox.Show("Please enter a branch name.",
                    "Empty name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git checkout -b {NewBranchName} {SelectedCommit.ShortHash}",
                "creates a new branch starting from this commit");

            var result = await _git.CreateBranchFromCommitAsync(
                NewBranchName, SelectedCommit.Hash);

            if (result.Success)
            {
                MessageBox.Show(
                    $"Branch '{NewBranchName}' created and switched to!",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                NewBranchName = "";
            }
            else
                MessageBox.Show("Could not create branch. Check the terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }
    }
}