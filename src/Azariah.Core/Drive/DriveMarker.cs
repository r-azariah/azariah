namespace Azariah.Core.Drive;

/// <summary>
/// Contents of <c>.azariah/drive.json</c>. This identifies the drive so the app can find its
/// root and recognize the same drive under a different letter. It is NOT a trust credential:
/// anyone can copy it. Trust comes from the cryptographic pairing designed for Phase 3.
/// </summary>
public sealed record DriveMarker
{
    public const int CurrentFormat = 1;

    public int Format { get; init; } = CurrentFormat;
    public Guid DriveId { get; init; }
    public string DisplayName { get; init; } = "Azariah";
    public DateTimeOffset CreatedUtc { get; init; }

    public static DriveMarker CreateNew(string? displayName = null) => new()
    {
        DriveId = Guid.NewGuid(),
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Azariah" : displayName.Trim(),
        CreatedUtc = DateTimeOffset.UtcNow,
    };
}
