using System.Text.Json.Serialization;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Launcher;
using Azariah.Core.Settings;

namespace Azariah.Core.Serialization;

/// <summary>Source-generated JSON metadata so persistence stays trim/AOT friendly.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(DriveMarker))]
[JsonSerializable(typeof(TrashInfo))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(RecentFilesDocument))]
[JsonSerializable(typeof(LauncherConfig))]
public sealed partial class AzariahJsonContext : JsonSerializerContext;
