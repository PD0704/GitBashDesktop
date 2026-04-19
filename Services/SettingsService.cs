using GitBashDesktop.Models;
using System;
using System.IO;
using System.Text.Json;

namespace GitBashDesktop.Services
{
    public class SettingsService
    {
        private readonly string _filePath;
        private AppSettings _current = new();

        public AppSettings Current => _current;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "GitBashDesktop");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return _current;
                var json = File.ReadAllText(_filePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json)
                    ?? new AppSettings();
                return _current;
            }
            catch { return _current; }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                _current = settings;
                var json = JsonSerializer.Serialize(settings,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public void Set<T>(Action<AppSettings> update)
        {
            update(_current);
            Save(_current);
        }
    }
}