using Shiranuix.PluginHost;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginManifestReader"/>: what the editor says about a
/// manifest it will not load.
/// </summary>
/// <remarks>
/// Every sentence matters here. A manifest is the only thing the editor can
/// read about a plugin before it runs any of that plugin's code, so this is
/// where an author's mistake has to be caught and said clearly.
/// </remarks>
public sealed class PluginManifestReaderTests {
    [Fact]
    public void A_Sound_Manifest_Is_Read_As_It_Was_Written() {
        using var plugins = new PluginFixture();
        var folder = plugins.WriteManifest(
            "word-count", "word-count", entryPoint: "ReadingTime.ReadingTimePlugin");

        var read = PluginManifestReader.Read(folder);

        Assert.True(read.Succeeded);
        Assert.Equal("word-count", read.Manifest!.Id);
        Assert.Equal("A test plugin", read.Manifest.Name);
        Assert.Equal(new Version(1, 0, 0), read.Manifest.Version);
        Assert.Equal("ReadingTime.ReadingTimePlugin", read.Manifest.EntryPoint);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void A_Folder_Without_A_Manifest_Is_Said_Not_Found() {
        using var plugins = new PluginFixture();
        var folder = plugins.FolderWithoutManifest("lonely");

        var read = PluginManifestReader.Read(folder);

        Assert.False(read.Succeeded);
        Assert.Contains(read.Problems, problem => problem.Contains("manifest.json is missing"));
    }

    [Fact]
    public void A_Manifest_That_Is_Not_JSON_Is_Said_So_RatherThan_Thrown() {
        using var plugins = new PluginFixture();
        var folder = plugins.Folder("broken");
        File.WriteAllText(Path.Combine(folder, "manifest.json"), "{ this is not json");

        var read = PluginManifestReader.Read(folder);

        Assert.False(read.Succeeded);
        Assert.Contains(read.Problems, problem => problem.Contains("could not be read"));
    }

    [Fact]
    public void Every_Problem_Is_Reported_Together() {
        using var plugins = new PluginFixture();
        var folder = plugins.Folder("empty");
        File.WriteAllText(Path.Combine(folder, "manifest.json"), "{ }");

        var read = PluginManifestReader.Read(folder);

        // One run to fix them: id, name, version, api and entry point.
        Assert.Equal(5, read.Problems.Count);
    }

    [Fact]
    public void A_Version_That_Is_Not_A_Version_Is_Said_So() {
        using var plugins = new PluginFixture();
        var folder = plugins.WriteManifest("odd", "odd", version: "one-point-oh");

        var read = PluginManifestReader.Read(folder);

        Assert.Contains(read.Problems, problem => problem.Contains("is not a version"));
    }

    [Fact]
    public void A_Contract_Range_That_Makes_No_Sense_Is_Said_So() {
        using var plugins = new PluginFixture();
        var folder = plugins.WriteManifest("odd", "odd", api: "soon");

        var read = PluginManifestReader.Read(folder);

        Assert.Contains(read.Problems, problem => problem.Contains("not a range"));
    }

    [Fact]
    public void A_Plugin_Filed_Under_Another_Plugins_Name_Is_Refused() {
        using var plugins = new PluginFixture();
        var folder = plugins.WriteManifest("someone-elses", "word-count");

        var read = PluginManifestReader.Read(folder);

        Assert.False(read.Succeeded);
        Assert.Contains(read.Problems, problem => problem.Contains("filed under 'someone-elses'"));
    }

    [Fact]
    public void Comments_And_Trailing_Commas_Are_Not_Refused() {
        // A manifest is a file people edit by hand; a hand may leave a comment.
        using var plugins = new PluginFixture();
        var folder = plugins.Folder("commented");
        File.WriteAllText(Path.Combine(folder, "manifest.json"), string.Join("\n", [
            "{",
            "  // the name a person reads",
            "  \"id\": \"commented\",",
            "  \"name\": \"Commented\",",
            "  \"version\": \"1.0.0\",",
            "  \"api\": \"0.1\",",
            "  \"entryPoint\": \"ReadingTime.ReadingTimePlugin\",",
            "}"
        ]));

        var read = PluginManifestReader.Read(folder);

        Assert.True(read.Succeeded);
    }
}
