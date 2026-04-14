namespace GitBashDesktop.Models
{
    public class CommitInfo
    {
        public string Hash { get; set; } = "";
        public string ShortHash { get; set; } = "";
        public string Author { get; set; } = "";
        public string Email { get; set; } = "";
        public string Date { get; set; } = "";
        public string Message { get; set; } = "";
        public string Branch { get; set; } = "";

        public string AvatarInitials => Author.Length >= 2
            ? $"{Author[0]}{Author.Split(' ').Last()[0]}".ToUpper()
            : Author.Length == 1
                ? Author[0].ToString().ToUpper()
                : "?";

        public string ShortMessage => Message.Length > 60
            ? Message[..60] + "..."
            : Message;
    }
}