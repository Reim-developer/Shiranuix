using Shiranuix.Plugins;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginId"/> and <see cref="PluginManifest"/>: the two
/// things a plugin has to get right before the host will touch its code.
/// </summary>
public sealed class PluginManifestTests {
    [Theory]
    [InlineData("word-count")]
    [InlineData("shiranuix.spell")]
    [InlineData("v2-notes")]
    [InlineData("ab")]
    public void An_Id_Is_Written_The_Same_way_On_Every_Machine(string id) {
        Assert.True(PluginId.IsValid(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Word-Count")]
    [InlineData("word count")]
    [InlineData("word_count")]
    [InlineData("-word")]
    public void An_Id_The_File_System_Could_Confuse_Is_Refused(string? id) {
        Assert.False(PluginId.IsValid(id));
    }

    [Fact]
    public void An_Id_Longer_Than_The_Cap_Is_Refused() {
        Assert.False(PluginId.IsValid(new string('a', PluginId.MaxLength + 1)));
        Assert.True(PluginId.IsValid(new string('a', PluginId.MaxLength)));
    }

    [Fact]
    public void A_Sound_Manifest_Has_Nothing_To_Complain_About() {
        Assert.Empty(SoundManifest().Validate());
    }

    [Fact]
    public void A_Manifest_With_No_Id_Says_How_An_Id_Must_Be_Written() {
        var manifest = SoundManifest() with { Id = "Word Count" };

        var problem = Assert.Single(manifest.Validate());

        Assert.Contains("lower-case letters", problem);
    }

    [Fact]
    public void A_Manifest_With_Nothing_To_Show_Is_Told_About_Every_Way_At_Once() {
        var manifest = SoundManifest() with {
            Id = string.Empty,
            Name = " ",
            EntryPoint = string.Empty
        };

        var problems = manifest.Validate();

        // One run to fix them, not three: the author should see the whole list.
        Assert.Equal(3, problems.Count);
    }

    [Fact]
    public void A_Manifest_That_Says_Which_Contract_It_Speaks_Is_Accepted() {
        var manifest = SoundManifest() with { Api = PluginApiRange.Parse("0.1 - 0.9") };

        Assert.Empty(manifest.Validate());
    }

    private static PluginManifest SoundManifest() => new(
        Id: "word-count",
        Name: "Word count",
        Version: new Version(1, 0, 0),
        Api: new PluginApiRange(new Version(0, 1)),
        EntryPoint: "Shiranuix.Plugins.WordCount.WordCountPlugin",
        Description: "Counts the words in the document.",
        Author: "Reim");
}
