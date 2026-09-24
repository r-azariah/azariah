using Azariah.Core.Drive;
using Azariah.Core.Serialization;

namespace Azariah.Core.Settings;

public sealed class SettingsStore(DriveLayout layout)
{
    public AppSettings Load() =>
        JsonFile.TryRead(layout.SettingsFile, AzariahJsonContext.Default.AppSettings) ?? new AppSettings();

    public void Save(AppSettings settings) =>
        JsonFile.WriteAtomic(layout.SettingsFile, settings, AzariahJsonContext.Default.AppSettings);
}
