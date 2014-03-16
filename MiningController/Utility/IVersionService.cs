using System;

namespace MiningController
{
    public interface IVersionService
    {
        bool IsUpdateAvailable();

        void IsUpdateAvailableAsync(Action<bool> callback);

        string CurrentVersion { get; }
    }
}
