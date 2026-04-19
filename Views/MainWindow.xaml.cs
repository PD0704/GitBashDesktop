using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GitBashDesktop.Services;

namespace GitBashDesktop.Views
{
    public partial class MainWindow : Window
    {
        private bool _isDark = true;
        private Button? _activeButton;
        internal DashboardView? _dashboardView;
        internal BranchesView? _branchesView;
        public static bool RepoIsOpen => Git.HasRepo;
        public bool RepoVisible => Git.HasRepo;
        // Shared across all views
        public static GitService Git { get; private set; } = new GitService();
        public static Action<string>? TerminalCallback { get; private set; }
        public static MainWindow? Instance { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            TerminalCallback = AppendTerminal;
            // Hook terminal output to the terminal panel
            Git.Initialize("", AppendTerminal);
            LoadGitUser();
            InitViews();
            NavigateTo("dashboard", null);
        }

        private void InitViews()
        {
            _dashboardView = new DashboardView(Git);
            _branchesView = new BranchesView(Git);
        }

        private void AppendTerminal(string text)
        {
            Dispatcher.Invoke(() =>
            {
                TerminalOutput.Text += text + "\n";
                TerminalScroller.ScrollToEnd();
            });
        }
        public static void UpdateSidebarRepo(string repoName)
        {
            Instance?.Dispatcher.Invoke(() =>
            {
                if (Instance == null) return;
                Instance.SidebarRepoName.Text = repoName;
                // Refresh binding
                Instance.MainWindowRoot?.InvalidateProperty(
                    Window.ContentProperty);
            });
        }
        private ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        public static void UpdateCommandBar(string command, string explanation)
        {
            Instance?.Dispatcher.Invoke(() =>
            {
                Instance.CommandPreviewText.Text = command;
                Instance.CommandExplainText.Text = explanation;
            });
        }

        private void LoadGitUser()
        {
            try
            {
                var name = RunGitCommand("config user.name").Trim();
                var email = RunGitCommand("config user.email").Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    AccountName.Text = name;
                    AvatarInitials.Text = GetInitials(name);
                }
                if (!string.IsNullOrEmpty(email))
                    AccountEmail.Text = email;
            }
            catch { }
        }

        private string GetInitials(string name)
        {
            var parts = name.Split(' ');
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
        }

        private void SidebarNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                NavigateTo(btn.Tag?.ToString(), btn);
        }

        private void NavigateTo(string? page, Button? btn)
        {
            // Block navigation to other pages if no repo open
            if (!Git.HasRepo && page != "dashboard" && page != "settings")
            {
                MessageBox.Show(
                    "Please open a repository first.",
                    "No repository", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Highlight active button
            if (_activeButton != null)
                _activeButton.Background = Brushes.Transparent;

            if (btn != null)
            {
                btn.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(_isDark ? "#37373D" : "#D0D0D0"));
                _activeButton = btn;
            }

            MainContent.Content = page switch
            {
                "dashboard" => _dashboardView,
                "branches" => Git.HasRepo ? _branchesView : _dashboardView,
                "history" => Git.HasRepo ? new CommitHistoryView(Git) : _dashboardView,
                "conflicts" => Git.HasRepo ? new MergeConflictsView(Git) : _dashboardView,
                "settings" => new SettingsView(),
                _ => _dashboardView
            };

            CommandPreviewText.Text = page switch
            {
                "dashboard" => "git status",
                "branches" => "git branch -a",
                "history" => "git log --oneline",
                "conflicts" => "git diff --name-only --diff-filter=U",
                "settings" => "git config --list",
                _ => "git status"
            };

            CommandExplainText.Text = page switch
            {
                "dashboard" => "shows working tree status",
                "branches" => "lists all local and remote branches",
                "history" => "shows commit history as one line each",
                "conflicts" => "lists files with merge conflicts",
                "settings" => "lists all git configuration",
                _ => "shows working tree status"
            };
        }

        private string RunGitCommand(string args)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            return process.StandardOutput.ReadToEnd();
        }

        private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            ApplyTheme(_isDark);
            ThemeToggleBtn.Content = _isDark ? "☀" : "☾";
        }

        private void ApplyTheme(bool dark)
        {
            var res = Application.Current.Resources;

            if (dark)
            {
                res["AppBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                res["SidebarBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"));
                res["SidebarBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
                res["SidebarHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2D2E"));
                res["TerminalBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C0C0C"));
                res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#858585"));
                res["TextWhiteBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                res["TerminalTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                res["TerminalMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#858585"));
                res["TerminalBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
            }
            else
            {
                res["AppBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                res["SidebarBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB"));
                res["SidebarBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                res["SidebarHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCDCDC"));
                res["TerminalBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
                res["TextWhiteBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                res["TerminalTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                res["TerminalMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
                res["TerminalBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            }

            // Update active button highlight to match new theme
            if (_activeButton != null)
            {
                _activeButton.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(dark ? "#37373D" : "#D0D0D0"));
            }
        }

        private void SwitchAccount_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Account switching coming soon!", "Switch Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void NotifyRepoOpened()
        {
            Instance?.Dispatcher.Invoke(() =>
            {
                Instance?.UpdateRepoState();
                if (Instance != null)
                    Instance.SidebarRepoName.Text = Git.RepoPath != ""
                        ? System.IO.Path.GetFileName(Git.RepoPath)
                        : "";
            });
        }

        private void UpdateRepoState()
        {
            // If no repo, force back to dashboard
            if (!Git.HasRepo)
                NavigateTo("dashboard", null);
        }

        public void ReinitBranchesView()
        {
            _branchesView = new BranchesView(Git);
        }
    }
}