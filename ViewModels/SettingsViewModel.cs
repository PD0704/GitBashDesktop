using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBashDesktop.Models;
using GitBashDesktop.Services;
using System.Threading.Tasks;
using System.Windows;

namespace GitBashDesktop.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly GitService _git;
        private readonly SettingsService _settings;

        [ObservableProperty] private string _gitUserName = "";
        [ObservableProperty] private string _gitUserEmail = "";
        [ObservableProperty] private bool _isExpertMode = false;
        [ObservableProperty] private bool _isDarkTheme = true;
        [ObservableProperty] private bool _autoOpenLastRepo = false;
        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private string _statusMessage = "";

        public SettingsViewModel(GitService git, SettingsService settings)
        {
            _git = git;
            _settings = settings;

            var s = settings.Current;
            // Read live value from MainWindow, not cached settings
            _isExpertMode = Views.MainWindow.IsExpertMode;
            _isDarkTheme = s.IsDarkTheme;
            _autoOpenLastRepo = s.AutoOpenLastRepo;

            _ = LoadGitConfigAsync();
        }

        private async Task LoadGitConfigAsync()
        {
            GitUserName = await _git.GetUserNameAsync();
            GitUserEmail = await _git.GetUserEmailAsync();
        }

        // ── Expert mode toggle ────────────────────────────────────────────────
        partial void OnIsExpertModeChanged(bool value)
        {
            Views.MainWindow.SetExpertMode(value);
            Views.MainWindow.Instance?.Dispatcher.Invoke(() =>
            {
                if (Views.MainWindow.Instance?.ExpertModeBtn != null)
                    Views.MainWindow.Instance.ExpertModeBtn.Opacity = value ? 1.0 : 0.4;
            });
            StatusMessage = value
                ? "Expert mode on — command explanations hidden"
                : "Beginner mode on — command explanations visible";
        }

        // ── Dark theme toggle ─────────────────────────────────────────────────
        partial void OnIsDarkThemeChanged(bool value)
        {
            Views.MainWindow.Instance?.Dispatcher.Invoke(() =>
            {
                Views.MainWindow.Instance?.SimulateThemeToggle(value);
            });
        }

        // ── Auto open toggle ──────────────────────────────────────────────────
        partial void OnAutoOpenLastRepoChanged(bool value)
        {
            _settings.Set<bool>(s => s.AutoOpenLastRepo = value);
            StatusMessage = value
                ? "App will open last repo on startup"
                : "App will show repo list on startup";
        }

        // ── Save git config ───────────────────────────────────────────────────
        [RelayCommand]
        private async Task SaveGitConfigAsync()
        {
            if (string.IsNullOrWhiteSpace(GitUserName) ||
                string.IsNullOrWhiteSpace(GitUserEmail))
            {
                MessageBox.Show("Name and email cannot be empty.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Views.MainWindow.UpdateCommandBar(
                $"git config --global user.name \"{GitUserName}\"",
                "sets your git identity globally");

            await _git.RunAsync($"config --global user.name \"{GitUserName}\"");
            await _git.RunAsync($"config --global user.email \"{GitUserEmail}\"");

            _settings.Set<string>(s =>
            {
                s.GitUserName = GitUserName;
                s.GitUserEmail = GitUserEmail;
            });

            // Update sidebar account section
            Views.MainWindow.Instance?.Dispatcher.Invoke(() =>
            {
                if (Views.MainWindow.Instance != null)
                {
                    Views.MainWindow.Instance.AccountName.Text = GitUserName;
                    Views.MainWindow.Instance.AccountEmail.Text = GitUserEmail;
                    Views.MainWindow.Instance.AvatarInitials.Text =
                        GitUserName.Length >= 2
                            ? $"{GitUserName[0]}{GitUserName.Split(' ').Last()[0]}"
                                .ToUpper()
                            : GitUserName.ToUpper();
                }
            });

            StatusMessage = "Git config saved successfully!";
            IsBusy = false;
        }

        // ── Reset to defaults ─────────────────────────────────────────────────
        [RelayCommand]
        private void ResetDefaults()
        {
            var confirm = MessageBox.Show(
                "Reset all settings to defaults?",
                "Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            IsExpertMode = false;
            IsDarkTheme = true;
            AutoOpenLastRepo = false;
            _settings.Save(new AppSettings());
            StatusMessage = "Settings reset to defaults.";
        }
    }
}