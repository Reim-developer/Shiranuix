using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.Graphs;

/// <summary>
/// One character as the canvas carries them: the id the store knows them
/// by, the wiki's own account of them, and where their node stands.
/// </summary>
public sealed partial class GraphNodeViewModel : ObservableObject {
    private double _x;
    private double _y;

    /// <summary>
    /// Creates a node over the character the store holds.
    /// </summary>
    /// <param name="id">The node's row.</param>
    /// <param name="wikiCharacterId">The wiki entry they stand for.</param>
    /// <param name="name">What they are called.</param>
    /// <param name="birthDate">The day they were born, as the writer writes it.</param>
    /// <param name="description">What the writer says about them.</param>
    /// <param name="note">Whatever else is worth keeping of them.</param>
    /// <param name="x">Where their node stands across.</param>
    /// <param name="y">Where their node stands down.</param>
    public GraphNodeViewModel(
        int id,
        int wikiCharacterId,
        string name,
        string birthDate,
        string description,
        string note,
        double x,
        double y) {
        Id = id;
        WikiCharacterId = wikiCharacterId;
        Name = name;
        BirthDate = birthDate;
        Description = description;
        Note = note;
        _x = x;
        _y = y;
    }

    /// <summary>The node's row in the store.</summary>
    public int Id { get; }

    /// <summary>The wiki entry this character stands for.</summary>
    public int WikiCharacterId { get; }

    /// <summary>What the character is called, drawn under their node.</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// The letter the node wears in its circle — the first of their name,
    /// as an avatar wears it.
    /// </summary>
    public string Initial => GraphInitial.Of(Name);

    /// <summary>The day they were born, as the writer writes it.</summary>
    [ObservableProperty]
    private string _birthDate;

    /// <summary>What the writer says about them.</summary>
    [ObservableProperty]
    private string _description;

    /// <summary>Whatever else is worth keeping of them.</summary>
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
