namespace Azariah.Core.Files;

public enum FileKind
{
    Folder,
    Text,
    Document,
    Pdf,
    Spreadsheet,
    Presentation,
    Code,
    LuauScript,
    RobloxPlace,
    RobloxModel,
    Image,
    Audio,
    Video,
    Archive,
    Installer,
    Executable,
    Other,
}

public static class FileKinds
{
    private static readonly Dictionary<string, FileKind> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = FileKind.Text, [".md"] = FileKind.Text, [".log"] = FileKind.Text, [".rtf"] = FileKind.Text,
        [".csv"] = FileKind.Spreadsheet, [".xlsx"] = FileKind.Spreadsheet, [".xls"] = FileKind.Spreadsheet, [".ods"] = FileKind.Spreadsheet,
        [".doc"] = FileKind.Document, [".docx"] = FileKind.Document, [".odt"] = FileKind.Document,
        [".pdf"] = FileKind.Pdf,
        [".ppt"] = FileKind.Presentation, [".pptx"] = FileKind.Presentation, [".odp"] = FileKind.Presentation,
        [".lua"] = FileKind.LuauScript, [".luau"] = FileKind.LuauScript,
        [".rbxl"] = FileKind.RobloxPlace, [".rbxlx"] = FileKind.RobloxPlace,
        [".rbxm"] = FileKind.RobloxModel, [".rbxmx"] = FileKind.RobloxModel,
        [".cs"] = FileKind.Code, [".py"] = FileKind.Code, [".js"] = FileKind.Code, [".ts"] = FileKind.Code,
        [".json"] = FileKind.Code, [".xml"] = FileKind.Code, [".yaml"] = FileKind.Code, [".yml"] = FileKind.Code,
        [".toml"] = FileKind.Code, [".html"] = FileKind.Code, [".css"] = FileKind.Code, [".ps1"] = FileKind.Code,
        [".sh"] = FileKind.Code, [".bat"] = FileKind.Code, [".cmd"] = FileKind.Code, [".c"] = FileKind.Code,
        [".cpp"] = FileKind.Code, [".h"] = FileKind.Code, [".java"] = FileKind.Code, [".rs"] = FileKind.Code,
        [".go"] = FileKind.Code, [".axaml"] = FileKind.Code, [".xaml"] = FileKind.Code, [".csproj"] = FileKind.Code,
        [".png"] = FileKind.Image, [".jpg"] = FileKind.Image, [".jpeg"] = FileKind.Image, [".gif"] = FileKind.Image,
        [".bmp"] = FileKind.Image, [".webp"] = FileKind.Image, [".svg"] = FileKind.Image, [".ico"] = FileKind.Image,
        [".tga"] = FileKind.Image, [".psd"] = FileKind.Image,
        [".mp3"] = FileKind.Audio, [".wav"] = FileKind.Audio, [".ogg"] = FileKind.Audio, [".flac"] = FileKind.Audio, [".m4a"] = FileKind.Audio,
        [".mp4"] = FileKind.Video, [".mkv"] = FileKind.Video, [".mov"] = FileKind.Video, [".webm"] = FileKind.Video, [".avi"] = FileKind.Video,
        [".zip"] = FileKind.Archive, [".7z"] = FileKind.Archive, [".rar"] = FileKind.Archive, [".tar"] = FileKind.Archive, [".gz"] = FileKind.Archive,
        [".msi"] = FileKind.Installer, [".msix"] = FileKind.Installer, [".msixbundle"] = FileKind.Installer,
        [".appx"] = FileKind.Installer, [".appxbundle"] = FileKind.Installer,
        [".exe"] = FileKind.Executable,
    };

    public static FileKind FromExtension(string? extension) =>
        extension is not null && ByExtension.TryGetValue(extension, out var kind) ? kind : FileKind.Other;

    public static FileKind FromPath(string path) => FromExtension(Path.GetExtension(path));

    /// <summary>Extensions treated as installers by the Setup Kit.</summary>
    public static bool IsInstallerCandidate(string path)
    {
        var kind = FromPath(path);
        return kind is FileKind.Installer or FileKind.Executable;
    }

    public static string Describe(FileKind kind, string extension) => kind switch
    {
        FileKind.Folder => "Folder",
        FileKind.RobloxPlace => "Roblox place",
        FileKind.RobloxModel => "Roblox model",
        FileKind.LuauScript => "Luau script",
        FileKind.Pdf => "PDF document",
        FileKind.Installer => "Installer package",
        FileKind.Executable => "Application",
        _ when !string.IsNullOrEmpty(extension) => $"{extension.TrimStart('.').ToUpperInvariant()} file",
        _ => "File",
    };
}
