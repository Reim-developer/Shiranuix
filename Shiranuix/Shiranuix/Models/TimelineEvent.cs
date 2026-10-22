namespace Shiranuix.Models;

using System;

/// <summary>
/// One event standing on a timeline: when it happened, what it is called,
/// and where it is placed in the story's history.
/// </summary>
public sealed class TimelineEvent {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>The graph this event belongs to.</summary>
    public int GraphId { get; set; }

    /// <summary>The graph itself, when a query has joined it.</summary>
    public CharacterGraph Graph { get; set; } = null!;

    /// <summary>The date or time string when the event occurred.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>What the event is called.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What happened during the event.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Where the event stands along the timeline.</summary>
    public double Position { get; set; }

    /// <summary>When the event was created, in UTC.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When the event was last updated, in UTC.</summary>
    public DateTime UpdatedUtc { get; set; }
}
