using System;
using System.ComponentModel;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface ISettingsService : INotifyPropertyChanged
    {
        bool AlwaysOnTop { get; set; }
        double WindowOpacity { get; set; }
        string Theme { get; set; } // "Auto", "Orange", "Blue", "Purple", "Green"

        /// <summary>
        /// Star Citizen in-game handle of the current user. Used by
        /// <c>ICrewSessionService.MyShare</c> to compute the caller's cut of a crew session
        /// payout. Empty by default; when empty, share calculations fall back to an equal
        /// split across the crew.
        /// </summary>
        string UserHandle { get; set; }

        void Save();
    }
}
