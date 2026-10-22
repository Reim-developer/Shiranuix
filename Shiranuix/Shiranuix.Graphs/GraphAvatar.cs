using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Shiranuix.Graphs;

/// <summary>
/// A character's face, read out of the picture the writer chose: the bytes
/// the wiki keeps, small enough to keep and sharp enough to draw.
/// </summary>
/// <remarks>
/// <para>
/// The picture is read at the size a face is drawn — a circle a little
/// wider than a hand — and kept as a picture of its own, so that the wiki
/// carries a face rather than a photograph of a wall. A writer's own
/// picture is theirs to choose; the size of it is the app's business.
/// </para>
/// <para>
/// And a picture the app cannot read is no picture at all: the letter
/// stands in for it, and the writer is told nothing about it, because a
/// face that cannot be drawn is not worth a word.
/// </para>
/// </remarks>
public static class GraphAvatar {
    /// <summary>
    /// The biggest a face is kept: a circle a little wider than a hand,
    /// which is where it is drawn.
    /// </summary>
    public const int MaxSize = 256;

    /// <summary>
    /// Reads a picture out of a file, at the size a face is drawn.
    /// </summary>
    /// <param name="path">The picture's file.</param>
    /// <returns>The picture's bytes, or nothing when it cannot be read.</returns>
    public static byte[]? Read(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        try {
            using var source = new Bitmap(path);

            var widest = Math.Max(source.PixelSize.Width, source.PixelSize.Height);
            var scale = widest > MaxSize ? MaxSize / (double)widest : 1;
            var size = new PixelSize(
                Math.Max(1, (int)Math.Round(source.PixelSize.Width * scale)),
                Math.Max(1, (int)Math.Round(source.PixelSize.Height * scale)));

            using var target = new RenderTargetBitmap(size);
            using (var context = target.CreateDrawingContext()) {
                context.DrawImage(source, new Rect(source.Size), new Rect(target.Size));
            }

            using var stream = new MemoryStream();
            target.Save(stream, new PngBitmapEncoderOptions());

            return stream.ToArray();
        } catch (Exception) {
            return null;
        }
    }
}
