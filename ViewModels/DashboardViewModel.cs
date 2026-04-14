using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Models;
using GitBashDesktop.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace GitBashDesktop.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly GitService _git;
        private readonly RepoStorageService _storage;

        [ObservableProperty] private string _repoName = "No repository open";
        [ObservableProperty] private string _currentBranch = "";
        [ObservableProperty] private string _commitMessage = "";
        [ObservableProperty] private bool _hasRepo = false;
        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private string _statusSummary = "";

        public ObservableCollection<FileEntry> ChangedFiles { get; } = new();
        public ObservableCollection<FileEntry> StagedFiles { get; } = new();
        public ObservableCollection<RecentRepo> RecentRepos { get; } = new();

        public DashboardViewModel(GitService git)
        {
            _git = git;
            _storage = new RepoStorageService();
            LoadRecentRepos();
        }

        // ── Recent repos ─────────────────────────────────────────────────────
        private void LoadRecentRepos()
        {
            RecentRepos.Clear();
            foreach (var repo in _storage.Load())
                RecentRepos.Add(repo);
        }

        [RelayCommand]
        private async Task OpenRecentAsync(RecentRepo repo)
        {
            if (!repo.Exists)
            {
                var remove = MessageBox.Show(
                    $"The folder '{repo.Path}' no longer exists.\nRemove it from the list?",
                    "Folder not found", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (remove == MessageBoxResult.Yes)
                {
                    _storage.Remove(repo.Path);
                    LoadRecentRepos();
                }
                return;
            }

            await OpenRepoFromPathAsync(repo.Path);
        }

        [RelayCommand]
        private void RemoveRecent(RecentRepo repo)
        {
            _storage.Remove(repo.Path);
            LoadRecentRepos();
        }

        // ── Open repo ─────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task OpenRepoAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a Git repository folder"
            };
            if (dialog.ShowDialog() != true) return;
            await OpenRepoFromPathAsync(dialog.FolderName);
        }

        private async Task OpenRepoFromPathAsync(string path)
        {
            var isRepo = await _git.IsGitRepoAsync(path);
            if (!isRepo)
            {
                var init = MessageBox.Show(
                    "This folder is not a git repository. Initialise it?",
                    "Not a repo", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (init == MessageBoxResult.Yes)
                    await _git.InitAsync(path);
                else
                    return;
            }

            _git.Initialize(path, Views.MainWindow.TerminalCallback ?? (_ => { }));

            HasRepo = true;
            RepoName = System.IO.Path.GetFileName(path);

            _storage.AddOrUpdate(path);
            LoadRecentRepos();

            Views.MainWindow.Instance?.ReinitBranchesView();
            Views.MainWindow.NotifyRepoOpened();

            await RefreshAsync();
        }
        [RelayCommand]
        private void ChangeRepoAsync()
        {
            HasRepo = false;
            RepoName = "No repository open";
            CurrentBranch = "";
            ChangedFiles.Clear();
            StagedFiles.Clear();
            LoadRecentRepos();
            Views.MainWindow.UpdateCommandBar("git status", "shows working tree status");
        }

        // ── Clone repo ────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task CloneRepoAsync()
        {
            var dialog = new Views.CloneDialog();
            if (dialog.ShowDialog() != true) return;

            var dialog2 = new OpenFolderDialog
            {
                Title = "Choose where to clone into"
            };
            if (dialog2.ShowDialog() != true) return;

            IsBusy = true;
            var targetPath = System.IO.Path.Combine(
                dialog2.FolderName,
                System.IO.Path.GetFileNameWithoutExtension(dialog.Url));

            var result = await _git.CloneAsync(dialog.Url, targetPath);

            if (result.Success)
            {
                await OpenRepoFromPathAsync(targetPath);
            }
            else
            {
                MessageBox.Show("Clone failed. Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            IsBusy = false;
        }

        // ── Refresh status ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!_git.HasRepo) return;

            IsBusy = true;
            ChangedFiles.Clear();
            StagedFiles.Clear();

            var branchResult = await _git.GetCurrentBranchAsync();
            CurrentBranch = branchResult.Success ? branchResult.Output : "unknown";

            var statusResult = await _git.StatusShortAsync();
            if (statusResult.Success)
                ParseStatus(statusResult.Output);

            StatusSummary = $"{StagedFiles.Count} staged · {ChangedFiles.Count} unstaged";
            IsBusy = false;
        }

        private void ParseStatus(string output)
        {
            foreach (var line in output.Split('\n'))
            {
                if (line.Length < 3) continue;
                var xy = line[..2];
                var file = line[3..].Trim();
                var x = xy[0];
                var y = xy[1];

                if (x != ' ' && x != '?')
                    StagedFiles.Add(new FileEntry(file, GetStatus(x), true));

                if (y != ' ')
                    ChangedFiles.Add(new FileEntry(file, GetStatus(y), false));
            }
        }

        private static string GetStatus(char code) => code switch
        {
            'M' => "Modified",
            'A' => "Added",
            'D' => "Deleted",
            'R' => "Renamed",
            'C' => "Copied",
            '?' => "Untracked",
            'U' => "Conflict",
            _ => "Changed"
        };

        // ── Stage ─────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task StageSelectedAsync()
        {
            foreach (var f in ChangedFiles)
                if (f.IsSelected)
                    await _git.AddAsync($"\"{f.FileName}\"");
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task StageAllAsync()
        {
            await _git.AddAllAsync();
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task UnstageAsync(FileEntry file)
        {
            await _git.RunAsync($"restore --staged \"{file.FileName}\"");
            await RefreshAsync();
        }

        // ── Commit ────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task CommitAsync()
        {
            if (string.IsNullOrWhiteSpace(CommitMessage))
            {
                MessageBox.Show("Please enter a commit message.",
                    "Empty message", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StagedFiles.Count == 0)
            {
                MessageBox.Show("No staged files. Stage some files first.",
                    "Nothing to commit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            var result = await _git.CommitAsync(CommitMessage);

            if (result.Success)
            {
                var msg = CommitMessage;
                CommitMessage = "";
                Views.MainWindow.UpdateCommandBar(
                    "git status", "shows working tree status");
                MessageBox.Show($"Committed successfully!\n\n\"{msg}\"",
                    "Commit done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Commit failed. Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await RefreshAsync();
            IsBusy = false;
        }

        partial void OnCommitMessageChanged(string value)
        {
            var message = string.IsNullOrWhiteSpace(value)
                ? "..."
                : value.Length > 30 ? value[..30] + "..." : value;

            Views.MainWindow.UpdateCommandBar(
                $"git commit -m \"{message}\"",
                "commits staged files with your message");
        }
    }

    // ── File entry model ──────────────────────────────────────────────────────
    public partial class FileEntry : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public string FileName { get; }
        public string Status { get; }
        public bool IsStaged { get; }

        public string StatusColor => Status switch
        {
            "Modified" => "#E2B714",
            "Added" => "#4EC994",
            "Deleted" => "#F47067",
            "Untracked" => "#8B949E",
            "Conflict" => "#FF6B6B",
            _ => "#8B949E"
        };

        public FileEntry(string fileName, string status, bool isStaged)
        {
            FileName = fileName;
            Status = status;
            IsStaged = isStaged;
        }
    }
}