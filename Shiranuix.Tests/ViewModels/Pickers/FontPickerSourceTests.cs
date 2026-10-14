using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels.Pickers;

namespace Shiranuix.Tests.ViewModels.Pickers;

/// <summary>
/// Tests for <see cref="FontPickerSource"/>, the font picker's provider.
/// </summary>
/// <remarks>
/// The picker is what the format band shows, and its ordering is a decision
/// rather than a sort: lately used first, then installed, and the document's own
/// typeface never missing however the machine is stocked.
/// </remarks>
public sealed class FontPickerSourceTests {
    private readonly FakeFontCatalog _catalog = new();
    private readonly FakeFontHistoryStore _history = new();
    private readonly FontPickerSource _picker;

    private string _current = "Georgia";

    public FontPickerSourceTests() =>
        _picker = new FontPickerSource(_catalog, _history, () => _current, () => 17);

    [Fact]
    public void The_Lately_Used_Come_First() {
        // Remembered oldest first: the store hands back most recent first, so
        // Verdana - picked last - should lead the picker.
        _history.Remember("Georgia", 17);
        _history.Remember("Verdana", 24);

        var fonts = _picker.Items.ToList();

        Assert.Equal("Verdana", fonts[0]);
        Assert.Equal("Georgia", fonts[1]);
    }

    [Fact]
    public void The_Installed_Follow_In_Their_Own_Order() {
        var fonts = _picker.Items.ToList();

        Assert.True(fonts.IndexOf("Consolas") < fonts.IndexOf("Georgia"));
    }

    [Fact]
    public void Nothing_Appears_Twice() {
        _history.Remember("Georgia", 17);

        Assert.Equal(1, _picker.Items.Count(font => font == "Georgia"));
    }

    [Fact]
    public void A_Typeface_The_Machine_Lacks_Is_Still_Offered() {
        _current = "Futura";

        Assert.Contains("Futura", _picker.Items);
        Assert.Equal(_catalog.Families.Count + 1, _picker.Items.Count);
    }

    [Fact]
    public void Choosing_A_Typeface_Records_It_With_Its_Size() {
        _picker.Use("Verdana");

        Assert.Equal(new[] { ("Verdana", 17d) }, _history.Uses.ToArray());
    }
}
