using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IDiscordAuthService? _discordAuth;

        public SettingsViewModel(ISettingsService settingsService)
            : this(settingsService, null)
        {
        }

        /// <summary>
        /// DI entry point. <paramref name="discordAuth"/> is optional so the app still
        /// boots cleanly when Discord isn't configured — the sign-in UI stays hidden
        /// and the user falls back to editing UserHandle directly.
        /// </summary>
        public SettingsViewModel(ISettingsService settingsService, IDiscordAuthService? discordAuth)
        {
            _settingsService = settingsService;
            _discordAuth = discordAuth;

            // Initialize from service
            _alwaysOnTop = _settingsService.AlwaysOnTop;
            _windowOpacity = _settingsService.WindowOpacity;
            _userHandle = _settingsService.UserHandle;

            // Map saved theme string to selection
            var savedTheme = _settingsService.Theme;
            _selectedTheme = Themes.FirstOrDefault(t => t.Value == savedTheme) ?? Themes.First();

            if (_discordAuth is not null)
            {
                _discordAuth.SignInChanged += OnDiscordSignInChanged;
                // Prime observable state from any already-loaded account.
                SyncFromDiscord();
            }
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

        /// <summary>
        /// Star Citizen handle used by Wave 5B's crew-session "My Share" calculation.
        /// Persisted through <see cref="ISettingsService.UserHandle"/> so the value
        /// survives restarts and is visible to every service that needs it.
        /// </summary>
        [ObservableProperty]
        private string _userHandle = string.Empty;

        partial void OnUserHandleChanged(string value)
        {
            _settingsService.UserHandle = value ?? string.Empty;
        }

        // -----------------------------------------------------------------------------
        // Wave 8A — Discord sign-in state
        // -----------------------------------------------------------------------------

        /// <summary>
        /// True when Discord auth is available AND the user is signed in. Drives the
        /// "signed-in card" visibility in the view.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDiscordSignedOut))]
        [NotifyPropertyChangedFor(nameof(IsUserHandleEditable))]
        private bool _isDiscordSignedIn;

        /// <summary>
        /// True when Discord auth is available AND the user is NOT signed in. Drives the
        /// "Sign in with Discord" button visibility.
        /// </summary>
        public bool IsDiscordSignedOut => IsDiscordAvailable && !IsDiscordSignedIn;

        /// <summary>True when Discord auth was wired at startup. Used to hide the whole panel when not.</summary>
        public bool IsDiscordAvailable => _discordAuth is not null;

        /// <summary>
        /// UserHandle is read-only whenever Discord owns it. Manual edits would be overwritten
        /// next time the Discord account refreshes anyway, so we lock the TextBox.
        /// </summary>
        public bool IsUserHandleEditable => !IsDiscordSignedIn;

        [ObservableProperty]
        private string _discordUsername = string.Empty;

        [ObservableProperty]
        private string _discordAvatarUrl = string.Empty;

        [ObservableProperty]
        private bool _isSigningIn;

        [RelayCommand]
        private async Task SignInAsync()
        {
            if (_discordAuth is null || IsSigningIn) return;

            try
            {
                IsSigningIn = true;
                var account = await _discordAuth.SignInAsync().ConfigureAwait(true);
                // Observable sync happens via SignInChanged, but also run synchronously
                // here so the UI updates even if the event handler races the completion.
                ApplyAccount(account);
            }
            catch (Exception ex)
            {
                // Surface failures so the user understands why nothing happened. We use
                // a MessageBox because Settings doesn't have a status-bar area.
                MessageBox.Show(
                    $"Discord sign-in failed: {ex.Message}",
                    "Sign in with Discord",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsSigningIn = false;
            }
        }

        [RelayCommand]
        private async Task SignOutAsync()
        {
            if (_discordAuth is null) return;

            await _discordAuth.SignOutAsync().ConfigureAwait(true);
            // Clear the handle so the user isn't left with a stale Discord username
            // in a field they can now edit manually.
            UserHandle = string.Empty;
        }

        private void OnDiscordSignInChanged(object? sender, EventArgs e)
        {
            // SignInChanged can fire on background threads (token refresh on startup,
            // cancellation cleanup). Marshal back to the UI thread so observable property
            // notifications hit WPF bindings safely.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                SyncFromDiscord();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(SyncFromDiscord), DispatcherPriority.Normal);
            }
        }

        private void SyncFromDiscord()
        {
            if (_discordAuth is null) return;

            var account = _discordAuth.CurrentAccount;
            if (account is null || !_discordAuth.IsSignedIn)
            {
                IsDiscordSignedIn = false;
                DiscordUsername = string.Empty;
                DiscordAvatarUrl = string.Empty;
            }
            else
            {
                ApplyAccount(account);
            }
        }

        private void ApplyAccount(DiscordAccount account)
        {
            IsDiscordSignedIn = true;
            DiscordUsername = string.IsNullOrEmpty(account.GlobalName) ? account.Username : account.GlobalName;
            DiscordAvatarUrl = BuildAvatarUrl(account);
            // Take ownership of the handle — Discord username becomes SC handle while signed in.
            UserHandle = account.Username;
        }

        /// <summary>
        /// Build the CDN URL for the user's avatar. Users without a custom avatar have
        /// a null <see cref="DiscordAccount.AvatarHash"/>; we return empty so the view
        /// can hide the image rather than show a broken link.
        /// </summary>
        private static string BuildAvatarUrl(DiscordAccount account)
        {
            if (string.IsNullOrEmpty(account.AvatarHash)) return string.Empty;
            return $"https://cdn.discordapp.com/avatars/{account.Id}/{account.AvatarHash}.png?size=64";
        }

        // -----------------------------------------------------------------------------
        // Theme
        // -----------------------------------------------------------------------------

        public class ThemeOption
        {
            public required string Name { get; set; }
            public required string Value { get; set; }
            public required string ColorHex { get; set; }
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
