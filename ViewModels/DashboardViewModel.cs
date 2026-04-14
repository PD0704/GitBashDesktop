using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Services;
using GitBashDesktop.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace GitBashDesktop.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly GitService _git;

        [ObservableProperty] private string _repoName = "No repository open";
        [ObservableProperty] private string _currentBranch = "";
        [ObservableProperty] private string _commitMessage = "";
        [ObservableProperty] private bool _hasRepo = false;
        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private string _statusSummary = "";

        public ObservableCollection<FileEntry> ChangedFiles { get; } = new();
        public ObservableCollection<FileEntry> StagedFiles { get; } = new();

        public DashboardViewModel(GitService git)
        {
            _git = git;
        }

        // ── Open repo ────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task OpenRepoAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a Git repository folder"
            };

            if (dialog.ShowDialog() != true) return;
            var path = dialog.FolderName;

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
            await RefreshAsync();
        }

        // ── Clone repo ───────────────────────────────────────────────────────
        [RelayCommand]
        private async Task CloneRepoAsync()
        {
            var dialog = new CloneDialog();
            if (dialog.ShowDialog() != true) return;

            var dialog2 = new OpenFolderDialog
            {
                Title = "Choose where to clone into"
            };
            if (dialog2.ShowDialog() != true) return;

            IsBusy = true;
            var result = await _git.CloneAsync(dialog.Url,
                System.IO.Path.Combine(dialog2.FolderName,
                    System.IO.Path.GetFileNameWithoutExtension(dialog.Url)));

            if (result.Success)
            {
                HasRepo = true;
                RepoName = System.IO.Path.GetFileName(_git.RepoPath);
                await RefreshAsync();
            }
            else
                MessageBox.Show("Clone failed. Check the terminal for details.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Refresh status ───────────────────────────────────────────────────
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

                // X = staged, Y = unstaged
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

        // ── Stage selected ───────────────────────────────────────────────────
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

        // ── Commit ───────────────────────────────────────────────────────────
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
                CommitMessage = "";

            await RefreshAsync();
            IsBusy = false;
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