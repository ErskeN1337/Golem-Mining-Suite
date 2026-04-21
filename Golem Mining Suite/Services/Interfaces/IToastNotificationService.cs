namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Thin abstraction over Windows 10/11 toast notifications so the rest of the app can
    /// fire system toasts without taking a direct dependency on
    /// <c>Microsoft.Toolkit.Uwp.Notifications</c>. Keeping the surface narrow also lets
    /// tests swap in a fake that records calls instead of popping OS-level UI.
    /// </summary>
    public interface IToastNotificationService
    {
        /// <summary>
        /// Fire the flagship "your refinery order is ready" toast. This is the single
        /// miner-facing notification Wave 5C exists to deliver.
        /// </summary>
        /// <param name="refineryName">Station where the order is waiting (e.g. "ARC-L1").</param>
        /// <param name="oreName">Refined commodity name (e.g. "Quantanium").</param>
        /// <param name="quantitySCU">Expected yield in SCU.</param>
        void ShowRefineryReady(string refineryName, string oreName, decimal quantitySCU);

        /// <summary>
        /// Generic informational toast. Use sparingly — this is not a log sink.
        /// </summary>
        void ShowInfo(string title, string message);

        /// <summary>
        /// Generic warning toast. Use for user-actionable issues (e.g. "UEX API unreachable").
        /// </summary>
        void ShowWarning(string title, string message);
    }
}
