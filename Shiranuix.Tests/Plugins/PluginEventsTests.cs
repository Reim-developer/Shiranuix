using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginEvents"/>: the channel plugins announce news over,
/// and the promises made about what happens when one of them misbehaves.
/// </summary>
public sealed class PluginEventsTests {
    /// <summary>A kind of news no part of the editor has ever heard of.</summary>
    private sealed record ChapterStarted(string Title);

    /// <summary>Another kind, to prove the channel keeps kinds apart.</summary>
    private sealed record ChapterFinished(string Title);

    private readonly RecordingLoggerFactory _log = new();

    [Fact]
    public void A_Listener_Hears_The_Kind_Of_News_It_Asked_For() {
        var events = new PluginEvents(_log);
        var heard = new List<ChapterStarted>();

        events.Subscribe<ChapterStarted>(news => heard.Add(news));
        events.Publish(new ChapterStarted("One"));

        Assert.Equal("One", Assert.Single(heard).Title);
    }

    [Fact]
    public void News_Of_One_Kind_Wakes_Nobody_Listening_For_Another() {
        var events = new PluginEvents(_log);
        var heard = new List<ChapterStarted>();

        events.Subscribe<ChapterStarted>(news => heard.Add(news));
        events.Publish(new ChapterFinished("One"));

        Assert.Empty(heard);

        // And the two kinds are kept apart rather than muddled together: the
        // listener is still there, waiting for its own news.
        Assert.Equal(1, events.ListenerCount<ChapterStarted>());
        Assert.Equal(0, events.ListenerCount<ChapterFinished>());
    }

    [Fact]
    public void Listeners_Are_Woken_In_The_Order_They_Arrived() {
        var events = new PluginEvents(_log);
        var order = new List<string>();

        events.Subscribe<ChapterStarted>(_ => order.Add("first"));
        events.Subscribe<ChapterStarted>(_ => order.Add("second"));
        events.Publish(new ChapterStarted("One"));

        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public void Dropping_The_Subscription_Stops_The_News() {
        var events = new PluginEvents(_log);
        var heard = 0;

        var subscription = events.Subscribe<ChapterStarted>(_ => heard++);
        events.Publish(new ChapterStarted("One"));

        subscription.Dispose();
        events.Publish(new ChapterStarted("Two"));

        Assert.Equal(1, heard);
        Assert.False(subscription.IsActive);
        Assert.Equal(0, events.ListenerCount<ChapterStarted>());
    }

    [Fact]
    public void A_Listener_That_Throws_Does_Not_Take_The_Rest_Down_With_It() {
        var events = new PluginEvents(_log);
        var heard = 0;

        events.Subscribe<ChapterStarted>(_ => throw new InvalidOperationException("no"));
        events.Subscribe<ChapterStarted>(_ => heard++);

        var outcome = events.Publish(new ChapterStarted("One"));

        // The other listener still ran, and the publisher is told what happened
        // so it can decide whether it matters to it.
        Assert.Equal(1, heard);
        Assert.False(outcome.IsSuccess);
        Assert.Contains("1 of 2", outcome.Error);
        Assert.True(_log.Mentions("threw"), "a listener's failure went unmentioned");
    }

    [Fact]
    public void News_Nobody_Is_Listening_For_Is_Not_A_Failure() {
        var events = new PluginEvents(_log);

        Assert.True(events.Publish(new ChapterStarted("One")).IsSuccess);
    }

    [Fact]
    public void Nothing_Is_Subscribed_As_Null() {
        var events = new PluginEvents(_log);

        Assert.Throws<ArgumentNullException>(
            () => events.Subscribe<ChapterStarted>(null!));
        Assert.Throws<ArgumentNullException>(
            () => events.Publish<ChapterStarted>(null!));
    }
}
