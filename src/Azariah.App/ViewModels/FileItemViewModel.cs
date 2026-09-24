using Azariah.App.Services;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace Azariah.App.ViewModels;

public sealed partial class FileItemViewModel(FileEntry entry, DriveLayout layout, bool showLocation = false) : ViewModelBase
{
    public FileEntry Entry { get; } = entry;
    public string FullPath => Entry.FullPath;
    public string Name => Entry.Name;
    public bool IsFolder => Entry.IsDirectory;
    public bool IsProtected => layout.IsProtected(Entry.FullPath);
    public MaterialIconKind Icon => IconFor(Entry.Kind, IsProtected);
    public string SizeText => Entry.Size is { } size ? Format.Bytes(size) : string.Empty;
    public string ModifiedText => Format.Date(Entry.ModifiedUtc);
    public string CreatedText => Format.Date(Entry.CreatedUtc);
    public string AgoText => Format.Ago(Entry.ModifiedUtc);
    public string TypeText => FileKinds.Describe(Entry.Kind, Entry.Extension);
    public bool ShowLocation { get; } = showLocation;

    public string LocationText
    {
        get
        {
            var parent = Path.GetDirectoryName(Entry.FullPath);
            if (parent is null || !layout.Contains(parent))
            {
                return parent ?? string.Empty;
            }

            var rel = layout.ToRelative(parent);
            return rel == "." ? "Drive" : rel;
        }
    }

    [ObservableProperty]
    public partial string? Sha256 { get; set; }

    [ObservableProperty]
    public partial bool IsHashing { get; set; }

    public static MaterialIconKind IconFor(FileKind kind, bool isProtected = false) => isProtected
        ? MaterialIconKind.ShieldLockOutline
        : kind switch
        {
            FileKind.Folder => MaterialIconKind.Folder,
            FileKind.Text => MaterialIconKind.FileDocumentOutline,
            FileKind.Document => MaterialIconKind.FileWord,
            FileKind.Pdf => MaterialIconKind.FilePdfBox,
            FileKind.Spreadsheet => MaterialIconKind.FileExcel,
            FileKind.Presentation => MaterialIconKind.FilePowerpoint,
            FileKind.Code => MaterialIconKind.FileCode,
            FileKind.LuauScript => MaterialIconKind.LanguageLua,
            FileKind.RobloxPlace => MaterialIconKind.CubeOutline,
            FileKind.RobloxModel => MaterialIconKind.CubeScan,
            FileKind.Image => MaterialIconKind.FileImage,
            FileKind.Audio => MaterialIconKind.FileMusic,
            FileKind.Video => MaterialIconKind.FileVideo,
            FileKind.Archive => MaterialIconKind.FolderZip,
            FileKind.Installer => MaterialIconKind.PackageDown,
            FileKind.Executable => MaterialIconKind.Application,
            _ => MaterialIconKind.FileOutline,
        };
}

public sealed record BreadcrumbItem(string Label, string Path, bool IsLast);
