using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Windows;
using System.Windows.Media;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Initialize from service
            _alwaysOnTop = _settingsService.AlwaysOnTop;
            _windowOpacity = _settingsService.WindowOpacity;
            
            // Map saved theme string to selection
            var savedTheme = _settingsService.Theme;
            _selectedTheme = Themes.FirstOrDefault(t => t.Value == savedTheme) ?? Themes.First();
        }

        [ObservableProperty]
        private bool _alwaysOnTop;

        partial void OnAlwaysOnTopChanged(bool value)
        {
            _settingsService.AlwaysOnTop = value;
        }

        [ObservableProperty]
        private double _windowOpacity;

        partial void OnWindowOpacityChanged(double value)
        {
            _settingsService.WindowOpacity = value;
        }

        public class ThemeOption
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string ColorHex { get; set; }
        }

        public ObservableCollection<ThemeOption> Themes { get; } = new ObservableCollection<ThemeOption>
        {
            new ThemeOption { Name = "Automatic (Mode Based)", Value = "Auto", ColorHex = "#888888" },
            new ThemeOption { Name = "Mining Orange", Value = "Orange", ColorHex = "#FF8C42" },
            new ThemeOption { Name = "Hauling Blue", Value = "Blue", ColorHex = "#4A90E2" },
            new ThemeOption { Name = "Purple", Value = "Purple", ColorHex = "#9C27B0" },
            new ThemeOption { Name = "Green", Value = "Green", ColorHex = "#4CAF50" },
            new ThemeOption { Name = "Red", Value = "Red", ColorHex = "#F44336" }
        };

        [ObservableProperty]
        private ThemeOption _selectedTheme;

        partial void OnSelectedThemeChanged(ThemeOption value)
        {
            if (value != null)
            {
                _settingsService.Theme = value.Value;
                ApplyTheme(value.Value);
            }
        }

        private void ApplyTheme(string themeValue)
        {
            if (themeValue == "Auto")
            {
                // Re-trigger mode based branding update if possible, 
                // but MainViewModel handles that. 
                // Actually, if we set it to Auto, we need to tell MainViewModel to re-apply current mode theme.
                // We'll use Messenger for this later if needed, or just let MainViewModel observe settings.
                // For now, let's just not override it here, or set it to default.
                return;
            }

            string colorHex = Themes.FirstOrDefault(t => t.Value == themeValue)?.ColorHex ?? "#FF8C42";
            
            Application.Current.Resources["AccentColor"] = (Color)ColorConverter.ConvertFromString(colorHex);
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }
    }
}
