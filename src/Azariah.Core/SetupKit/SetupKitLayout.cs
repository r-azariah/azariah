namespace Azariah.Core.SetupKit;

/// <summary>
/// Folder structure of the portable Setup Kit. The Setup Kit is public, unencrypted storage
/// and is usable without unlocking anything, so it must never contain secrets.
/// New AI apps or tools are added by adding folders (and, later, manifest files), not code.
/// </summary>
public static class SetupKitLayout
{
    public const string InstallersFolder = "Installers";
    public const string SkillsFolder = "Skills";
    public const string ConfigsFolder = "Configs";
    public const string DocsFolder = "Docs";

    public static readonly IReadOnlyList<string> InstallerCategories =
        ["AI Apps", "Developer Tools", "Roblox Tools", "Utilities", "Other"];

    public static readonly IReadOnlyList<string> SkillTools = ["Claude Code", "ChatGPT"];

    private const string NoSecretsRule =
        "Never put passwords, API keys, tokens, cookies, account credentials or recovery keys here. " +
        "The Setup Kit is not encrypted. Secrets belong in the Vault.";

    public static void EnsureCreated(string setupKitRoot)
    {
        Directory.CreateDirectory(setupKitRoot);
        WriteIfMissing(Path.Combine(setupKitRoot, "README.md"),
            "# Setup Kit\n\nPortable installers, AI skills, config templates and setup docs for setting up a PC.\n\n" +
            "- `Installers/`: one folder per category. Nothing here ever runs automatically; launch installers " +
            "yourself from the Setup Kit page, which shows the details first.\n" +
            "- `Skills/`: reusable AI skills, prompts and instructions, one folder per tool.\n" +
            "- `Configs/`: configuration templates.\n" +
            "- `Docs/`: setup notes and guides.\n\n" +
            $"**{NoSecretsRule}**\n");

        foreach (var category in InstallerCategories)
        {
            Directory.CreateDirectory(Path.Combine(setupKitRoot, InstallersFolder, category));
        }

        WriteIfMissing(Path.Combine(setupKitRoot, InstallersFolder, "README.md"),
            "# Installers\n\nKeep official installers here, sorted by category. Download them from the " +
            "vendor's official site and note the version and date. If the vendor publishes a SHA-256 " +
            "checksum, record it so the file can be verified before launching.\n\n" +
            "Suggested: `AI Apps/ChatGPT Desktop`, `AI Apps/Claude Desktop`, `Developer Tools/Claude Code`, " +
            "`Developer Tools/Git`, `Roblox Tools/Rojo`.\n\n" + NoSecretsRule + "\n");

        foreach (var tool in SkillTools)
        {
            Directory.CreateDirectory(Path.Combine(setupKitRoot, SkillsFolder, tool));
        }

        WriteIfMissing(Path.Combine(setupKitRoot, SkillsFolder, "README.md"),
            "# Skills\n\nReusable AI skills, prompts, instructions and templates, one folder per tool " +
            "(`Claude Code`, `ChatGPT`, ...). For Claude Code, each skill is a folder containing a " +
            "`SKILL.md` (it installs to `%USERPROFILE%\\.claude\\skills\\<name>`).\n\n" + NoSecretsRule + "\n");

        Directory.CreateDirectory(Path.Combine(setupKitRoot, ConfigsFolder));
        Directory.CreateDirectory(Path.Combine(setupKitRoot, DocsFolder));
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
        }
    }
}
