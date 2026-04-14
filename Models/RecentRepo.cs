using System;

namespace GitBashDesktop.Models
{
    public class RecentRepo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public DateTime LastOpened { get; set; } = DateTime.Now;
        public bool Exists => System.IO.Directory.Exists(Path);
    }
}