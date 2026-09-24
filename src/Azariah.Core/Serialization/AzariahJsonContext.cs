using System.Text.Json.Serialization;
using Azariah.Core.Drive;
using Azariah.Core.Files;

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
public sealed partial class AzariahJsonContext : JsonSerializerContext;
