using GitBashDesktop.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GitBashDesktop.Services
{
    public class RepoStorageService
    {
        private const int MaxRepos = 5;
        private readonly string _filePath;

        public RepoStorageService()
        {
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "GitBashDesktop");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "repos.json");
        }

        public List<RecentRepo> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<RecentRepo>>(json) ?? new();
            }
            catch { return new(); }
        }

        public void Save(List<RecentRepo> repos)
        {
            try
            {
                var json = JsonSerializer.Serialize(repos,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public void AddOrUpdate(string path)
        {
            var repos = Load();
            var name = System.IO.Path.GetFileName(path);

            // Remove if already exists
            repos.RemoveAll(r => r.Path.Equals(path,
                StringComparison.OrdinalIgnoreCase));

            // Add to top
            repos.Insert(0, new RecentRepo
            {
                Name = name,
                Path = path,
                LastOpened = DateTime.Now
            });

            // Keep only max 5
            if (repos.Count > MaxRepos)
                repos = repos.Take(MaxRepos).ToList();

            Save(repos);
        }

        public void Remove(string path)
        {
            var repos = Load();
            repos.RemoveAll(r => r.Path.Equals(path,
                StringComparison.OrdinalIgnoreCase));
            Save(repos);
        }
    }
}