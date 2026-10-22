using Avalonia.Media.Imaging;

namespace Shiranuix.Graphs;

/// <summary>
/// The pictures the map draws, remembered once they have been read out of
/// their bytes: a character's face is decoded once and drawn as many times
/// as the paper is redrawn.
/// </summary>
/// <remarks>
/// <para>
/// A face is bytes, and bytes are not a picture until something reads them
/// — which is work worth doing once and not worth doing again for every
/// frame the paper is drawn in. So the picture is kept beside the bytes it
/// came from, and the same bytes draw the same picture.
/// </para>
/// <para>
/// And a picture that will not decode leaves the letter standing: a face
/// the app cannot read is not a face, and a character whose picture is
/// broken is still a character.
/// </para>
/// </remarks>
public sealed class GraphImageCache : IDisposable {
    private readonly Dictionary<int, (byte[] Bytes, Bitmap? Face)> _faces = new();

    /// <summary>
    /// The picture a character's bytes stand for — or nothing, for a
    /// character whose face the writer has not chosen.
    /// </summary>
    /// <param name="id">The character whose face is wanted.</param>
    /// <param name="bytes">The picture's bytes, or nothing.</param>
    /// <returns>The picture, or nothing.</returns>
    public Bitmap? Face(int id, byte[]? bytes) {
        if (bytes is null || bytes.Length == 0) {
            _faces.Remove(id);
            return null;
        }

        // The same bytes draw the same picture: the bytes are the key, and
        // a face the writer has changed is a different set of bytes.
        if (_faces.TryGetValue(id, out var known) && ReferenceEquals(known.Bytes, bytes)) {
            return known.Face;
        }

        var face = Decode(bytes);
        _faces[id] = (bytes, face);

        return face;
    }

    /// <summary>
    /// Reads a picture out of its bytes, or gives up quietly: a face the
    /// app cannot read is a face the letter stands in for.
    /// </summary>
    /// <param name="bytes">The picture's bytes.</param>
    /// <returns>The picture, or nothing.</returns>
    private static Bitmap? Decode(byte[] bytes) {
        try {
            return new Bitmap(new MemoryStream(bytes));
        } catch (Exception) {
            return null;
        }
    }

    /// <summary>
    /// Lets go of every picture read: the papers that drew them are gone,
    /// and the pictures they held go with them.
    /// </summary>
    public void Dispose() {
        foreach (var entry in _faces.Values) {
            entry.Face?.Dispose();
        }

        _faces.Clear();
    }
}
