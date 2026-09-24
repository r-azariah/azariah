namespace Azariah.Core.Files;

/// <summary>A file operation was refused or failed in a way that should be shown to the user.</summary>
public sealed class FileOperationException : Exception
{
    public FileOperationException()
    {
    }

    public FileOperationException(string message)
        : base(message)
    {
    }

    public FileOperationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
