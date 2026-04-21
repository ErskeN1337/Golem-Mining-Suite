using System;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Windows 10/11 toast implementation of <see cref="IToastNotificationService"/> built on
    /// <c>Microsoft.Toolkit.Uwp.Notifications</c> 7.x. The 7.x line is the last pre-WinUI
    /// migration release and is the generally-accepted choice for packaged-or-unpackaged
    /// desktop apps on <c>net8.0-windows</c> — it works without requiring the WinRT /
    /// WindowsAppSDK project-file scaffolding.
    /// </summary>
    /// <remarks>
    /// All toast invocations are wrapped in try/catch because Windows can reject toasts in a
    /// number of transient situations (Focus Assist on, notifications disabled per-app,
    /// COM activation failure for unpackaged apps, etc.). A failed toast must never crash
    /// the process — at worst the user just doesn't see their refinery alert.
    /// </remarks>
    public sealed class ToastNotificationService : IToastNotificationService
    {
        private readonly ILogger<ToastNotificationService> _logger;

        public ToastNotificationService(ILogger<ToastNotificationService> logger)
        {
            _logger = logger;
        }

        public void ShowRefineryReady(string refineryName, string oreName, decimal quantitySCU)
        {
            // Matches the pattern in the task spec: one-line headline + one-line subtitle that
            // packs refinery + quantity + commodity. The "view-refinery" argument is the hook
            // for a future "clicking the toast opens the refinery tab" wire-up; harmless today.
            try
            {
                new ToastContentBuilder()
                    .AddArgument("action", "view-refinery")
                    .AddArgument("refinery", refineryName)
                    .AddText("Refinery order ready")
                    .AddText($"{quantitySCU:0.##} SCU of {oreName} at {refineryName}")
                    .Show();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to show refinery-ready toast for {Ore} at {Refinery}",
                    oreName, refineryName);
            }
        }

        public void ShowInfo(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show info toast {Title}", title);
            }
        }

        public void ShowWarning(string title, string message)
        {
            try
            {
                // The 7.x builder has no dedicated "warning" glyph — the title line carries the
                // severity. Keeping the call symmetrical with ShowInfo keeps callers simple.
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show warning toast {Title}", title);
            }
        }
    }
}
