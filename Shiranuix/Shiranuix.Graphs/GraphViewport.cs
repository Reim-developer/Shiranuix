using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// The camera the map is seen through: the world's own units magnified or
/// shrunk by a zoom, and carried onto the glass by an offset.
/// </summary>
/// <remarks>
/// The zoom is the writer's own — the wheel changes it, and the paper keeps
/// it — but everything else here is the map's: the world's middle stands in
/// the glass's middle until the writer says otherwise, and a graph that
/// has just been arranged is brought whole into the glass so that no part
/// of it stands off the paper.
/// </remarks>
public sealed class GraphViewport : IGraphViewport {
    private double _zoom = 1;
    private Point _offset;

    /// <inheritdoc />
    public double Zoom => _zoom;

    /// <inheritdoc />
    public Point Offset => _offset;

    /// <inheritdoc />
    public bool HasCentered { get; private set; }

    /// <inheritdoc />
    public Point ToScreen(Point world) =>
        (Point)((Vector)world * _zoom) + _offset;

    /// <inheritdoc />
    public Point ToWorld(Point screen) =>
        (Point)((Vector)(screen - _offset) / _zoom);

    /// <inheritdoc />
    public void ZoomAt(Point screen, double factor) {
        // The place under the hand stays under the hand: the world is read
        // out of the glass before the glass is changed, and put back into it
        // afterwards around that same place.
        var world = ToWorld(screen);

        _zoom = Math.Clamp(_zoom * factor, GraphMetrics.MinZoom, GraphMetrics.MaxZoom);
        _offset = screen - (Vector)world * _zoom;
    }

    /// <inheritdoc />
    public void PanBy(Vector delta) => _offset += delta;

    /// <inheritdoc />
    public void CenterOn(Rect world, Size glass) {
        _offset = new Point(
            glass.Width / 2 - (world.X + world.Width / 2) * _zoom,
            glass.Height / 2 - (world.Y + world.Height / 2) * _zoom);

        HasCentered = true;
    }

    /// <inheritdoc />
    public void Fit(Rect world, Size glass, double padding) {
        // The room kept around the graph, so that no circle and no name
        // stands on the glass's own edge.
        var width = world.Width + padding * 2;
        var height = world.Height + padding * 2;

        if (width <= 0 || height <= 0 || glass.Width <= 0 || glass.Height <= 0) {
            return;
        }

        // Never closer than the world stands by itself: a small graph is
        // shown at its own size in the middle of the glass, not blown up.
        var zoom = Math.Clamp(
            Math.Min(glass.Width / width, glass.Height / height),
            GraphMetrics.MinZoom,
            1);

        _zoom = zoom;
        _offset = new Point(
            glass.Width / 2 - (world.X + world.Width / 2) * zoom,
            glass.Height / 2 - (world.Y + world.Height / 2) * zoom);

        HasCentered = true;
    }
}
