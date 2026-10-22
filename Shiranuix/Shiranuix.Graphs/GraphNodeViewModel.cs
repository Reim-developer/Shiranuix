using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.Graphs;

/// <summary>
/// One character as the canvas carries them: the id the store knows them
/// by, the name and note the writer gave, and where their node stands.
/// </summary>
public sealed partial class GraphNodeViewModel : ObservableObject {
    private double _x;
    private double _y;

    /// <summary>
    /// Creates a node over the character the store holds.
    /// </summary>
    /// <param name="id">The character's row.</param>
    /// <param name="name">What they are called.</param>
    /// <param name="note">What the writer remembers about them.</param>
    /// <param name="x">Where their node stands across.</param>
    /// <param name="y">Where their node stands down.</param>
    public GraphNodeViewModel(int id, string name, string note, double x, double y) {
        Id = id;
        Name = name;
        Note = note;
        _x = x;
        _y = y;
    }

    /// <summary>The character's row in the store.</summary>
    public int Id { get; }

    /// <summary>What the character is called, drawn under their node.</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// The letter the node wears in its circle — the first of their name,
    /// as an avatar wears it.
    /// </summary>
    public string Initial => GraphInitial.Of(Name);

    /// <summary>What the writer remembers about them.</summary>
    [ObservableProperty]
    private string _note;

    /// <summary>Where the node stands across, in the canvas's world units.</summary>
    public double X {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>Where the node stands down, in the canvas's world units.</summary>
    public double Y {
        get => _y;
        set => SetProperty(ref _y, value);
    }
}
