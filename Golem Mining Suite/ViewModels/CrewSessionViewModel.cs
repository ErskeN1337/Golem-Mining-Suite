using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models.Regolith;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Golem_Mining_Suite.ViewModels
{
    /// <summary>
    /// View-model for <c>CrewSessionView</c>. Wraps <see cref="ICrewSessionService"/> and
    /// <see cref="IRegolithImporter"/> so the UI can import, inspect, and delete local
    /// crew sessions without taking direct service dependencies.
    /// </summary>
    public partial class CrewSessionViewModel : ObservableObject, IDisposable
    {
        private readonly ICrewSessionService _sessionService;
        private readonly IRegolithImporter _importer;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<CrewSessionViewModel> _logger;
        private bool _disposed;

        [ObservableProperty]
        private ObservableCollection<CrewSessionRow> _sessions = new();

        [ObservableProperty]
        private CrewSessionRow? _selectedSession;

        [ObservableProperty]
        private string _statusText = "No sessions imported yet. Use the buttons above to get started.";

        [ObservableProperty]
        private bool _isBusy;

        public CrewSessionViewModel(
            ICrewSessionService sessionService,
            IRegolithImporter importer,
            ISettingsService settingsService,
            ILogger<CrewSessionViewModel> logger)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // React to any change in the underlying store (import, delete, external reload).
            _sessionService.SessionsChanged += OnSessionsChanged;

            // Seed the grid from whatever the service already has in memory (App.xaml.cs
            // calls LoadAsync at startup, so by the time the user opens the view there
            // may already be data).
            RebuildRows();
        }

        // -----------------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Open a file picker so the user can drop in a Regolith per-session JSON export,
        /// parse it, and persist the result via <see cref="ICrewSessionService"/>.
        /// </summary>
        [RelayCommand]
        private async Task ImportFromRegolithFileAsync()
        {
            if (IsBusy) return;

            var dialog = new OpenFileDialog
            {
                Title = "Import Regolith Session",
                Filter = "Regolith session JSON (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            bool? picked = dialog.ShowDialog();
            if (picked != true) return;

            IsBusy = true;
            StatusText = $"Importing {System.IO.Path.GetFileName(dialog.FileName)}...";
            try
            {
                var result = await _importer.ImportFromFileAsync(dialog.FileName).ConfigureAwait(true);
                if (result.Sessions.Count == 0)
                {
                    StatusText = $"No sessions parsed. {FormatWarnings(result.Warnings)}".TrimEnd();
                    return;
                }

                await _sessionService.AddRangeAsync(result.Sessions).ConfigureAwait(true);
                StatusText = $"Imported {result.SessionsImported} session(s), "
                             + $"{result.WorkOrdersImported} work order(s), "
                             + $"{FormatAuec(result.TotalAuec)}. {FormatWarnings(result.Warnings)}".TrimEnd();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File import failed for {Path}", dialog.FileName);
                StatusText = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Pull every session the user has access to from Regolith's live API. The API key
        /// is supplied by the caller — the view prompts for it via an input box.
        /// </summary>
        [RelayCommand]
        private async Task ImportFromRegolithApiAsync(string? apiKey)
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Simple inline prompt — matches the "small preferences dialog" guidance in
                // the task spec without pulling in a new XAML window just for one field.
                var input = ApiKeyPrompt.Prompt();
                if (string.IsNullOrWhiteSpace(input))
                {
                    StatusText = "Import cancelled — no API key provided.";
                    return;
                }

                apiKey = input;
            }

            IsBusy = true;
            StatusText = "Contacting Regolith API...";
            try
            {
                var result = await _importer.ImportAllFromApiAsync(apiKey).ConfigureAwait(true);
                if (result.Sessions.Count == 0)
                {
                    StatusText = $"No sessions returned. {FormatWarnings(result.Warnings)}".TrimEnd();
                    return;
                }

                await _sessionService.AddRangeAsync(result.Sessions).ConfigureAwait(true);
                StatusText = $"Imported {result.SessionsImported} session(s), "
                             + $"{result.WorkOrdersImported} work order(s), "
                             + $"{FormatAuec(result.TotalAuec)}. {FormatWarnings(result.Warnings)}".TrimEnd();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API import failed");
                StatusText = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Confirm with the user, then drop the selected session from the store.</summary>
        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var selected = SelectedSession;
            if (selected is null)
            {
                StatusText = "Nothing to delete — select a session first.";
                return;
            }

            var answer = MessageBox.Show(
                $"Delete session '{selected.Name}'?\n\nThis only removes it from your local store — the original Regolith record is untouched.",
                "Confirm Delete",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.OK) return;

            IsBusy = true;
            try
            {
                await _sessionService.RemoveAsync(selected.Session.Id).ConfigureAwait(true);
                StatusText = $"Deleted '{selected.Name}'.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Delete failed for session {SessionId}", selected.Session.Id);
                StatusText = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Reload the session list from disk — useful after external edits.</summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusText = "Refreshing...";
            try
            {
                await _sessionService.LoadAsync().ConfigureAwait(true);
                StatusText = Sessions.Count == 0
                    ? "No sessions imported yet. Use the buttons above to get started."
                    : $"Loaded {Sessions.Count} session(s) from disk.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Refresh failed");
                StatusText = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // -----------------------------------------------------------------------------
        // Plumbing
        // -----------------------------------------------------------------------------

        private void OnSessionsChanged(object? sender, EventArgs e)
        {
            // Marshal to UI thread — the service can fire this from a worker thread and
            // ObservableCollection mutations are not thread-safe.
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(RebuildRows);
            }
            else
            {
                RebuildRows();
            }
        }

        private void RebuildRows()
        {
            var myHandle = _settingsService.UserHandle ?? string.Empty;
            var rows = _sessionService.Sessions
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new CrewSessionRow(s, _sessionService.MyShare(s, myHandle)))
                .ToList();

            Sessions.Clear();
            foreach (var row in rows)
            {
                Sessions.Add(row);
            }
        }

        private static string FormatAuec(decimal auec) =>
            $"{auec.ToString("N0", CultureInfo.InvariantCulture)} aUEC";

        private static string FormatWarnings(IReadOnlyList<string> warnings)
        {
            if (warnings is null || warnings.Count == 0) return string.Empty;
            return warnings.Count == 1
                ? $"Warning: {warnings[0]}"
                : $"{warnings.Count} warnings (see log).";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sessionService.SessionsChanged -= OnSessionsChanged;
        }
    }

    /// <summary>
    /// Minimal single-field WPF prompt built in code to avoid adding a whole XAML window
    /// just for a one-shot API-key capture. Modal against the active window, returns null
    /// if the user cancels.
    /// </summary>
    internal static class ApiKeyPrompt
    {
        public static string? Prompt()
        {
            var window = new Window
            {
                Title = "Import from Regolith API",
                Width = 480,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
            };

            var panel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(16),
            };

            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Paste your Regolith personal API key (x-api-key header).",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
            });
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Found in Regolith -> Account Settings.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = 0.7,
            });

            var textBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(6),
            };
            panel.Children.Add(textBox);

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            string? captured = null;

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true,
            };
            okButton.Click += (_, _) =>
            {
                captured = textBox.Text;
                window.DialogResult = true;
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                IsCancel = true,
            };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);

            window.Content = panel;
            textBox.Focus();
            bool? confirmed = window.ShowDialog();
            return confirmed == true ? captured : null;
        }
    }

    /// <summary>
    /// View-only wrapper around <see cref="ImportedSession"/> carrying pre-formatted
    /// fields for the DataGrid. Keeps formatting out of XAML and the service layer.
    /// </summary>
    public sealed record CrewSessionRow(ImportedSession Session, decimal MyShareAuec)
    {
        public string Name => Session.Name;
        public DateTime Started => Session.StartedAt.ToLocalTime();
        public int CrewCount => Session.Crew.Count;
        public decimal TotalAuec => Session.TotalPayoutAuec;
        public string SourceTool => Session.SourceTool;

        public string TotalAuecDisplay =>
            $"{TotalAuec.ToString("N0", CultureInfo.InvariantCulture)} aUEC";

        public string MyShareDisplay =>
            $"{MyShareAuec.ToString("N0", CultureInfo.InvariantCulture)} aUEC";

        public string StartedDisplay =>
            Started.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
