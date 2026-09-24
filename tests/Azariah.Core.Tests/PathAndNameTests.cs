using Azariah.Core.Drive;
using Azariah.Core.Files;

namespace Azariah.Core.Tests;

public class PathAndNameTests
{
    [Fact]
    public void Sibling_with_same_prefix_is_not_inside()
    {
        var root = Path.Combine(Path.GetTempPath(), "Drive");
        Assert.True(PathGuard.IsInsideOrEqual(root, Path.Combine(root, "a", "b")));
        Assert.False(PathGuard.IsInsideOrEqual(root, root + "X"));
        Assert.False(PathGuard.IsInsideOrEqual(root, Path.Combine(root, "..", "Other")));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("a:b")]
    [InlineData("what?")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("")]
    [InlineData("..")]
    public void Invalid_windows_names_are_rejected(string name) => Assert.False(FileNameRules.IsValid(name));

    [Theory]
    [InlineData("Game v2.rbxl")]
    [InlineData("notes (1).md")]
    [InlineData(".hidden")]
    public void Normal_names_are_accepted(string name) => Assert.True(FileNameRules.IsValid(name));

    [Fact]
    public void Layout_refuses_relative_paths_that_escape_the_root()
    {
        var layout = new DriveLayout(Path.Combine(Path.GetTempPath(), "Drive"));
        Assert.Throws<ArgumentException>(() => layout.ToFull(Path.Combine("..", "..", "secrets.txt")));
        Assert.EndsWith(Path.Combine("Files", "a.txt"), layout.ToFull(Path.Combine("Files", "a.txt")));
    }

    [Fact]
    public void System_and_vault_areas_are_protected()
    {
        var layout = new DriveLayout(Path.Combine(Path.GetTempPath(), "Drive"));
        Assert.True(layout.IsProtected(Path.Combine(layout.Vault, "blob")));
        Assert.True(layout.IsProtected(layout.MarkerFile));
        Assert.False(layout.IsProtected(Path.Combine(layout.Files, "x")));
    }
}
