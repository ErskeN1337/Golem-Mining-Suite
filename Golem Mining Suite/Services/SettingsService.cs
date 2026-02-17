using System;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Golem_Mining_Suite.Services.Interfaces;

namespace Golem_Mining_Suite.Services
{
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "user_settings.json";
        private string _settingsPath;

        private SettingsData _data;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SettingsService()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            Load();
        }

        public bool AlwaysOnTop
        {
            get => _data.AlwaysOnTop;
            set
            {
                if (_data.AlwaysOnTop != value)
                {
                    _data.AlwaysOnTop = value;
                    OnPropertyChanged();
                    Save();
                }
            }
        }

        public double WindowOpacity
        {
            get => _data.WindowOpacity;
            set
            {
                if (Math.Abs(_data.WindowOpacity - value) > 0.01)
                {
                    _data.WindowOpacity = value;
                    OnPropertyChanged();
                    Save();
                }
            }
        }

        public string Theme
        {
            get => _data.Theme;
            set
            {
                if (_data.Theme != value)
                {
                    _data.Theme = value;
                    OnPropertyChanged();
                    Save();
                }
            }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                }
                else
                {
                    _data = new SettingsData();
                }
            }
            catch
            {
                _data = new SettingsData();
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_data, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class SettingsData
        {
            public bool AlwaysOnTop { get; set; } = false;
            public double WindowOpacity { get; set; } = 1.0;
            public string Theme { get; set; } = "Auto";
        }
    }
}
