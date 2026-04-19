using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Models;
using GitBashDesktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GitBashDesktop.ViewModels
{
    public partial class MergeConflictsViewModel : ObservableObject
    {
        private readonly GitService _git;

        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private ConflictFile? _selectedFile = null;
        [ObservableProperty] private ConflictBlock? _selectedBlock = null;
        [ObservableProperty] private bool _hasMerge = false;
        [ObservableProperty] private string _mergeStatus = "";

        public ObservableCollection<ConflictFile> ConflictFiles { get; } = new();

        public MergeConflictsViewModel(GitService git)
        {
            _git = git;
            _ = LoadConflictsAsync();
        }

        // ── Load conflicts ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task LoadConflictsAsync()
        {
            if (!_git.HasRepo) return;

            IsBusy = true;
            ConflictFiles.Clear();
            SelectedFile = null;
            SelectedBlock = null;

            Views.MainWindow.UpdateCommandBar(
                "git diff --name-only --diff-filter=U",
                "lists files with merge conflicts");

            var result = await _git.GetConflictedFilesAsync();

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                HasMerge = false;
                MergeStatus = "No merge conflicts found.";
                IsBusy = false;
                return;
            }

            HasMerge = true;
            foreach (var line in result.Output.Split('\n'))
            {
                var path = line.Trim();
                if (string.IsNullOrEmpty(path)) continue;

                var normalizedPath = path
                    .Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .Trim();
                var fullPath = System.IO.Path.Combine(_git.RepoPath, normalizedPath);

                // Delete/delete conflict — auto resolve
                if (!File.Exists(fullPath))
                {
                    await _git.RunAsync($"rm \"{path}\"");
                    continue;
                }

                var conflictFile = new ConflictFile
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path)
                };

                await ParseConflictsAsync(conflictFile);

                // Delete/modify conflict — file exists but has no conflict markers
                if (conflictFile.Blocks.Count == 0)
                {
                    conflictFile.IsDeleteModifyConflict = true;
                }

                ConflictFiles.Add(conflictFile);
            }

            MergeStatus = $"{ConflictFiles.Count} file(s) with conflicts";
            IsBusy = false;
        }

        // ── Parse conflict blocks from file ───────────────────────────────────
        private async Task ParseConflictsAsync(ConflictFile file)
        {
            var fullPath = Path.Combine(_git.RepoPath, file.FilePath);
            if (!File.Exists(fullPath)) return;

            var lines = await File.ReadAllLinesAsync(fullPath);
            var blocks = new System.Collections.Generic.List<ConflictBlock>();

            var inOurs = false;
            var inTheirs = false;
            var ours = new StringBuilder();
            var theirs = new StringBuilder();
            var index = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("<<<<<<<"))
                {
                    inOurs = true;
                    ours.Clear();
                    theirs.Clear();
                }
                else if (line.StartsWith("=======") && inOurs)
                {
                    inOurs = false;
                    inTheirs = true;
                }
                else if (line.StartsWith(">>>>>>>") && inTheirs)
                {
                    inTheirs = false;
                    blocks.Add(new ConflictBlock
                    {
                        Index = index++,
                        OursText = ours.ToString().TrimEnd(),
                        TheirsText = theirs.ToString().TrimEnd()
                    });
                }
                else if (inOurs)
                    ours.AppendLine(line);
                else if (inTheirs)
                    theirs.AppendLine(line);
            }

            foreach (var b in blocks)
                file.Blocks.Add(b);
        }

        // ── Select file ───────────────────────────────────────────────────────
        [RelayCommand]
        private void SelectFile(ConflictFile file)
        {
            SelectedFile = file;
            SelectedBlock = null;

            if (!file.IsDeleteModifyConflict)
                SelectedBlock = file.Blocks.FirstOrDefault();

            Views.MainWindow.UpdateCommandBar(
                $"git add \"{file.FilePath}\"",
                "marks file as resolved after fixing conflicts");
        }

        // ── Select block ──────────────────────────────────────────────────────
        [RelayCommand]
        private void SelectBlock(ConflictBlock block)
            => SelectedBlock = block;

        // ── Resolve block ─────────────────────────────────────────────────────
        [RelayCommand]
        private void ResolveOurs()
        {
            if (SelectedBlock == null) return;
            SelectedBlock.Resolution = Resolution.Ours;
            MoveToNextBlock();
            RefreshFileStatus();
        }

        [RelayCommand]
        private void ResolveTheirs()
        {
            if (SelectedBlock == null) return;
            SelectedBlock.Resolution = Resolution.Theirs;
            MoveToNextBlock();
            RefreshFileStatus();
        }

        [RelayCommand]
        private void ResolveBoth()
        {
            if (SelectedBlock == null) return;
            SelectedBlock.Resolution = Resolution.Both;
            MoveToNextBlock();
            RefreshFileStatus();
        }

        private void MoveToNextBlock()
        {
            if (SelectedFile == null) return;
            var current = SelectedBlock;
            var next = SelectedFile.Blocks
                .FirstOrDefault(b => b != current && !b.IsResolved);
            if (next != null)
                SelectedBlock = next;
        }

        private void RefreshFileStatus()
        {
            if (SelectedFile == null) return;
            OnPropertyChanged(nameof(SelectedFile));
        }

        // ── Apply resolution to file ──────────────────────────────────────────
        [RelayCommand]
        private async Task ApplyResolutionAsync()
        {
            if (SelectedFile == null) return;

            if (!SelectedFile.AllResolved)
            {
                MessageBox.Show(
                    "Please resolve all conflicts in this file first.",
                    "Unresolved conflicts",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;

            // Rewrite the file with resolved content
            var fullPath = Path.Combine(_git.RepoPath, SelectedFile.FilePath);
            var lines = await File.ReadAllLinesAsync(fullPath);
            var output = new StringBuilder();
            var blockIndex = 0;
            var inConflict = false;
            var inOurs = false;
            var inTheirs = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("<<<<<<<"))
                {
                    inConflict = true;
                    inOurs = true;
                }
                else if (line.StartsWith("=======") && inConflict)
                {
                    inOurs = false;
                    inTheirs = true;
                }
                else if (line.StartsWith(">>>>>>>") && inConflict)
                {
                    // Write resolved content
                    if (blockIndex < SelectedFile.Blocks.Count)
                    {
                        var block = SelectedFile.Blocks[blockIndex++];
                        output.AppendLine(block.ResolvedText);
                    }
                    inConflict = false;
                    inOurs = false;
                    inTheirs = false;
                }
                else if (!inConflict)
                    output.AppendLine(line);
            }

            await File.WriteAllTextAsync(fullPath, output.ToString());

            // Mark as resolved in git
            var result = await _git.MarkResolvedAsync(SelectedFile.FilePath);

            if (result.Success)
            {
                MessageBox.Show(
                    $"'{SelectedFile.FileName}' resolved and staged!",
                    "Resolved", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadConflictsAsync();
            }
            else
                MessageBox.Show("Could not stage file. Check the terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Abort merge ───────────────────────────────────────────────────────
        [RelayCommand]
        private async Task AbortMergeAsync()
        {
            var confirm = MessageBox.Show(
                "Abort the merge? All conflict resolutions will be lost.",
                "Abort merge", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                "git merge --abort",
                "aborts the merge and restores previous state");

            var result = await _git.AbortMergeAsync();

            if (result.Success)
            {
                MessageBox.Show("Merge aborted successfully.",
                    "Aborted", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadConflictsAsync();
            }
            else
                MessageBox.Show("Could not abort merge. Check the terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        // ── Resolve Delete Modify Async  ─────────────────────────────────────────────────
        [RelayCommand]
        private async Task KeepFileAsync()
        {
            if (SelectedFile == null) return;
            IsBusy = true;

            Views.MainWindow.UpdateCommandBar(
                $"git add \"{SelectedFile.FilePath}\"",
                "keeps the modified file and stages it");

            var result = await _git.MarkResolvedAsync(SelectedFile.FilePath);
            if (result.Success)
            {
                MessageBox.Show(
                    $"'{SelectedFile.FileName}' kept and staged.",
                    "Resolved", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadConflictsAsync();
            }
            else
                MessageBox.Show("Could not stage file. Check terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }

        [RelayCommand]
        private async Task DeleteFileAsync()
        {
            if (SelectedFile == null) return;

            var confirm = MessageBox.Show(
                $"Delete '{SelectedFile.FileName}'?\nThis will remove it from the repo.",
                "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git rm \"{SelectedFile.FilePath}\"",
                "deletes the file and stages the removal");

            var result = await _git.RunAsync($"rm \"{SelectedFile.FilePath}\"");
            if (result.Success)
            {
                MessageBox.Show(
                    $"'{SelectedFile.FileName}' deleted and staged.",
                    "Resolved", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadConflictsAsync();
            }
            else
                MessageBox.Show("Could not delete file. Check terminal.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            IsBusy = false;
        }
    }
}