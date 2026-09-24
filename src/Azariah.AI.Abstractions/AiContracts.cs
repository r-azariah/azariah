using System.Text.Json;

namespace Azariah.AI;

// Phase 7 contracts. Nothing here is implemented yet; these types pin down the seams so the
// rest of the app can grow toward the assistant without being coupled to any one AI vendor.
//
// Two ways an AI "brain" can use Azariah, both going through the same tools and permissions:
//   1. MCP host: Azariah runs a local MCP server and an MCP client (e.g. Claude Desktop or
//      Claude Code, running on the user's own subscription) calls Azariah's tools.
//   2. In-app assistant: Azariah calls an IAiProvider (local model or cloud API) itself.

/// <summary>Things the assistant can be allowed to do. Each one is switched on separately.</summary>
public enum AiCapability
{
    SeeScreen,
    ReadUsbFiles,
    ReadVault,
    OpenFiles,
    ModifyFiles,
    ExecuteActions,
}

public enum PermissionState
{
    /// <summary>The capability is off. Tools needing it are not even offered to the model.</summary>
    Disabled,

    /// <summary>Every use shows a confirmation the user must accept.</summary>
    AskEveryTime,

    /// <summary>Allowed without asking (never the default for ModifyFiles/ExecuteActions/ReadVault).</summary>
    Allowed,
}

/// <summary>How private a piece of data is. Routing uses this to keep Vault data off the cloud unless allowed.</summary>
public enum DataSensitivity
{
    Public,
    Personal,
    Vault,
}

public interface IAiPermissionPolicy
{
    PermissionState Get(AiCapability capability);

    /// <summary>
    /// Returns true if the action may proceed. Implementations prompt the user for
    /// <see cref="PermissionState.AskEveryTime"/> and must fail closed (false) on any error.
    /// ReadVault additionally requires the Vault to be unlocked.
    /// </summary>
    Task<bool> RequestAsync(AiCapability capability, string reason, CancellationToken ct);
}

/// <summary>A tool the assistant can call, e.g. "search_usb_files" or "capture_screen".</summary>
public interface IAiTool
{
    string Name { get; }

    string Description { get; }

    /// <summary>JSON Schema for the arguments.</summary>
    string InputSchemaJson { get; }

    IReadOnlySet<AiCapability> RequiredCapabilities { get; }

    Task<AiToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct);
}

public sealed record AiToolResult(bool IsError, IReadOnlyList<AiContentPart> Content, DataSensitivity Sensitivity);

public enum AiRole
{
    System,
    User,
    Assistant,
    Tool,
}

public abstract record AiContentPart;

public sealed record AiTextPart(string Text) : AiContentPart;

/// <summary>An image such as a screenshot the user explicitly asked to share.</summary>
public sealed record AiImagePart(ReadOnlyMemory<byte> Data, string MediaType) : AiContentPart;

public sealed record AiMessage(AiRole Role, IReadOnlyList<AiContentPart> Content);

public sealed record AiRequest(
    IReadOnlyList<AiMessage> Messages,
    IReadOnlyList<IAiTool> Tools,
    DataSensitivity Sensitivity);

public sealed record AiResponse(AiMessage Message, string ProviderId);

public sealed record AiProviderInfo(string Id, string DisplayName, bool IsLocal, bool SupportsImages, bool SupportsTools);

/// <summary>A model backend: local model, Anthropic, OpenAI, ... The rest of the app never cares which.</summary>
public interface IAiProvider
{
    AiProviderInfo Info { get; }

    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct);
}

/// <summary>Picks a provider per request (e.g. local for Vault data or offline, cloud for hard questions).</summary>
public interface IAiRouter
{
    IAiProvider Select(AiRequest request);
}
