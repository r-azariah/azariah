namespace Azariah.Core.Files;

public enum ConflictPolicy
{
    /// <summary>Keep the existing item and write the new one as "name (2)".</summary>
    KeepBoth,

    /// <summary>Overwrite files (safely, via a temp copy) and merge folders.</summary>
    Replace,

    /// <summary>Leave the existing item alone and skip this one.</summary>
    Skip,
}

public sealed record FileProgress(
    string CurrentItem,
    long BytesDone,
    long BytesTotal,
    int ItemsDone,
    int ItemsTotal)
{
    public double Fraction => BytesTotal > 0 ? Math.Clamp((double)BytesDone / BytesTotal, 0, 1)
        : ItemsTotal > 0 ? Math.Clamp((double)ItemsDone / ItemsTotal, 0, 1) : 0;
}

public sealed record OperationError(string Path, string Message);

public sealed class OperationReport
{
    private readonly List<OperationError> _errors = [];
    private readonly List<string> _createdPaths = [];

    public int Succeeded { get; internal set; }
    public int Skipped { get; internal set; }
    public bool Cancelled { get; internal set; }
    public IReadOnlyList<OperationError> Errors => _errors;

    /// <summary>Top-level destination paths that were written, e.g. to select them afterwards.</summary>
    public IReadOnlyList<string> CreatedPaths => _createdPaths;

    public bool HasErrors => _errors.Count > 0;

    internal void AddError(string path, string message) => _errors.Add(new OperationError(path, message));

    internal void AddError(string path, Exception ex) => AddError(path, Describe(ex));

    internal void AddCreated(string path) => _createdPaths.Add(path);

    public static string Describe(Exception ex) => ex switch
    {
        FileOperationException => ex.Message,
        UnauthorizedAccessException => "Access denied.",
        PathTooLongException => "The path is too long.",
        DirectoryNotFoundException => "The folder no longer exists.",
        FileNotFoundException => "The file no longer exists.",
        IOException io when io.HResult == unchecked((int)0x80070020) => "The file is open in another program.",
        IOException io when io.HResult == unchecked((int)0x80070070) => "There isn't enough space on the drive.",
        IOException => ex.Message,
        _ => ex.Message,
    };
}
