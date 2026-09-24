namespace Azariah.App.Services;

/// <summary>Copy/cut state shared by every file browser in the session.</summary>
public sealed class FileClipboard
{
    public IReadOnlyList<string> Paths { get; private set; } = [];

    public bool IsCut { get; private set; }

    public bool HasItems => Paths.Count > 0;

    public event EventHandler? Changed;

    public void Set(IReadOnlyList<string> paths, bool cut)
    {
        Paths = paths;
        IsCut = cut;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() => Set([], false);
}
