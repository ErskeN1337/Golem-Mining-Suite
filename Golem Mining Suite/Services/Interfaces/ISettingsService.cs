using System;
using System.ComponentModel;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface ISettingsService : INotifyPropertyChanged
    {
        bool AlwaysOnTop { get; set; }
        double WindowOpacity { get; set; }
        string Theme { get; set; } // "Auto", "Orange", "Blue", "Purple", "Green"
        
        void Save();
    }
}
