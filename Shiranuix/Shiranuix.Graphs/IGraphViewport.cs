using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// Where the world stands on the glass: the zoom the writer has chosen and
/// the offset that carries the world's own units onto the canvas's pixels.
/// </summary>
/// <remarks>
/// The camera is a thing of its own so that everything that asks "where is
/// that place, on the glass?" asks one object and gets one answer. The
/// drawing, the hit test and the arrangement all read the same camera, so
/// a node cannot be drawn in one place and pressed in another.
/// </remarks>
public interface IGraphViewport {
    /// <summary>How much the world is magnified, or shrunk, on the glass.</summary>
    double Zoom { get; }

    /// <summary>Where the world's own origin stands on the glass.</summary>
    Point Offset { get; }

    /// <summary>Whether the paper has been stood up in the glass yet.</summary>
    bool HasCentered { get; }

    /// <summary>Where a place in the world stands on the glass.</summary>
    /// <param name="world">The place in the world.</param>
    /// <returns>The same place, on the glass.</returns>
    Point ToScreen(Point world);

    /// <summary>Where a point on the glass stands in the world.</summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The same place, in the world's own units.</returns>
    Point ToWorld(Point screen);

    /// <summary>Looks closer or further, about where the hand stands.</summary>
    /// <param name="screen">The point on the glass that stays where it is.</param>
    /// <param name="factor">How much to magnify by; below one pulls back.</param>
    void ZoomAt(Point screen, double factor);

    /// <summary>Moves the paper itself under the hand.</summary>
    /// <param name="delta">How far the paper moves, in the glass's own pixels.</param>
    void PanBy(Vector delta);

    /// <summary>Stands the middle of the world in the middle of the glass.</summary>
    /// <param name="world">The part of the world to stand in the middle of.</param>
    /// <param name="glass">The glass it stands in.</param>
    void CenterOn(Rect world, Size glass);

    /// <summary>
    /// Stands a part of the world whole inside the glass: the zoom that
    /// brings it all in, and the offset that puts its middle in the
    /// glass's middle. Never looks closer than the world stands by itself.
    /// </summary>
    /// <param name="world">The part of the world to bring in.</param>
    /// <param name="glass">The glass it has to fit inside.</param>
    /// <param name="padding">The room to keep around it, in the world's own units.</param>
    void Fit(Rect world, Size glass, double padding);
}
