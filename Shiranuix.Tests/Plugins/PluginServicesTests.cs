using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Services;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginServices"/>: the register plugins offer on, and
/// the rule that keeps one plugin from displacing another's.
/// </summary>
public sealed class PluginServicesTests {
    /// <summary>A service of a plugin's own invention.</summary>
    private interface IWordList {
        /// <summary>What it holds.</summary>
        string Words { get; }
    }

    private sealed class WordList(string words) : IWordList {
        public string Words => words;
    }

    private sealed class OtherList : IWordList {
        public string Words => "other";
    }

    private readonly Dictionary<Type, object> _doors = new() {
        [typeof(IEditorHost)] = new StubEditorHost()
    };

    private PluginServices Create() {
        var services = new PluginServices();

        // What the catalog does before any plugin runs: the editor's doors go
        // in first, so no plugin can replace them.
        foreach (var door in _doors) {
            services.Register(door.Key, door.Value);
        }

        return services;
    }

    [Fact]
    public void A_Service_A_Plugin_Offers_Is_There_For_It_And_For_Everyone_Else() {
        var services = Create();

        services.Register<IWordList>(new WordList("mùa lá"));

        Assert.True(services.TryResolve<IWordList>(out var found));
        Assert.Equal("mùa lá", found!.Words);
        Assert.True(services.IsRegistered<IWordList>());
    }

    [Fact]
    public void A_Type_Nobody_Registered_Comes_Back_As_Nothing() {
        var services = Create();

        Assert.False(services.TryResolve<IWordList>(out var found));
        Assert.Null(found);
        Assert.Null(services.Resolve<IWordList>());
        Assert.False(services.IsRegistered<IWordList>());
    }

    [Fact]
    public void The_First_One_In_Stays() {
        var services = Create();

        services.Register<IWordList>(new WordList("first"));
        services.Register<IWordList>(new OtherList());

        // First one wins: the register is a shelf, not a slot the last hand
        // reaches past the first one to take.
        Assert.Equal("first", services.Resolve<IWordList>()!.Words);
    }

    [Fact]
    public void A_Plugin_Cannot_Bring_Its_Own_Editor() {
        var services = Create();

        services.Register<IEditorHost>(new StubEditorHost());

        // The editor put its own door in the register before any plugin ran.
        Assert.Same(_doors[typeof(IEditorHost)], services.Resolve<IEditorHost>());
    }

    [Fact]
    public void The_Type_Only_Known_At_Run_Time_Is_Registered_Just_As_Well() {
        var services = Create();

        services.Register(typeof(IWordList), new WordList("by type"));

        Assert.Equal("by type", services.Resolve<IWordList>()!.Words);
    }

    [Fact]
    public void Nothing_Is_Registered_As_Null() {
        var services = Create();

        Assert.Throws<ArgumentNullException>(() => services.Register<IWordList>(null!));
        Assert.Throws<ArgumentNullException>(() => services.Register(typeof(IWordList), null!));
    }

    [Fact]
    public void A_Service_Cannot_Be_Registered_Under_A_Type_It_Is_Not() {
        var services = Create();

        Assert.Throws<ArgumentException>(
            () => services.Register(typeof(IWordList), "not a word list at all"));
    }
}
