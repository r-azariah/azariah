using Azariah.AI;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed record CapabilityRow(AiCapability Capability, string Title, string Description, MaterialIconKind Icon);

/// <summary>Preview of the AZARIAH assistant (Phase 7). Every capability is off until built and switched on.</summary>
public sealed class AiViewModel : ViewModelBase
{
    public IReadOnlyList<CapabilityRow> Capabilities { get; } =
    [
        new(AiCapability.SeeScreen, "See screen", "Only when you press the hotkey. Never records in the background.", MaterialIconKind.MonitorScreenshot),
        new(AiCapability.ReadUsbFiles, "Read USB files", "Search and summarize files and Roblox projects on the drive.", MaterialIconKind.FileSearch),
        new(AiCapability.ReadVault, "Read Vault", "Needs the Vault unlocked and your say-so.", MaterialIconKind.ShieldLockOutline),
        new(AiCapability.OpenFiles, "Open files", "Open things for you in their normal app.", MaterialIconKind.OpenInNew),
        new(AiCapability.ModifyFiles, "Modify files", "Rename, move or organize, with a confirmation each time.", MaterialIconKind.FileEdit),
        new(AiCapability.ExecuteActions, "Execute actions", "Run approved actions on this PC, one confirmation at a time.", MaterialIconKind.PlayCircleOutline),
    ];
}
