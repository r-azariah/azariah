using Material.Icons;

namespace Azariah.App.ViewModels;

/// <summary>Placeholder pages for features scheduled in later phases.</summary>
public sealed class ComingSoonViewModel : ViewModelBase
{
    public required string Title { get; init; }
    public required MaterialIconKind Icon { get; init; }
    public required string Phase { get; init; }
    public required string Headline { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Points { get; init; } = [];

    public static ComingSoonViewModel Vault() => new()
    {
        Title = "Vault",
        Icon = MaterialIconKind.ShieldLockOutline,
        Phase = "PHASE 2",
        Headline = "Encrypted storage for your private files",
        Description = "Everything in the Vault is encrypted on the drive. Without your master password it's unreadable, even if someone copies every byte.",
        Points =
        [
            "AES-256-GCM authenticated encryption, one key per file",
            "Argon2id password hashing; your master password is never stored",
            "Locks when you unplug the drive, close AZARIAH, lock it yourself, or go idle",
            "Damage to one file never takes down the rest of the Vault",
            "Optional recovery key in case you forget your password",
        ],
    };

    public static ComingSoonViewModel Passwords() => new()
    {
        Title = "Passwords",
        Icon = MaterialIconKind.KeyVariant,
        Phase = "PHASE 5",
        Headline = "A password manager that lives on your drive",
        Description = "Built on the KeePass (KDBX 4) format, so your passwords can also be opened with KeePassXC if you ever need a backup way in.",
        Points =
        [
            "Accounts, usernames, passwords, URLs and notes",
            "Search and one-click copy",
            "Clipboard clears itself after a few seconds",
            "Never stored in plaintext",
        ],
    };
}
