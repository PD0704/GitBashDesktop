namespace GitBashDesktop.Models
{
    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = true;
        public bool IsExpertMode { get; set; } = false;
        public bool AutoOpenLastRepo { get; set; } = false;
        public string LastRepoPath { get; set; } = "";
        public string GitUserName { get; set; } = "";
        public string GitUserEmail { get; set; } = "";
    }
}