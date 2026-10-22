using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Shiranuix.Graphs;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// A character's face, read out of the picture the writer chose: the
/// picture is kept at the size a face is drawn, and a picture that is not
/// there is no picture at all.
/// </summary>
[Collection("Headless UI")]
public sealed class GraphAvatarTests {
    [Fact]
    public void A_Picture_That_Is_Not_There_Is_No_Picture() {
        Assert.Null(GraphAvatar.Read(Path.Combine(Path.GetTempPath(), "no-such-picture.png")));
        Assert.Null(GraphAvatar.Read(string.Empty));
    }

    [Fact]
    public void A_Picture_Is_Kept_At_The_Size_A_Face_Is_Drawn() =>
        HeadlessUi.On(() => {
            var path = Path.Combine(Path.GetTempPath(), $"shiranuix-face-{Guid.NewGuid():N}.png");

            try {
                WritePicture(path, 512);

                var bytes = GraphAvatar.Read(path);

                Assert.NotNull(bytes);

                using var face = new Bitmap(new MemoryStream(bytes!));
                Assert.True(face.PixelSize.Width <= GraphAvatar.MaxSize);
                Assert.True(face.PixelSize.Height <= GraphAvatar.MaxSize);
            } finally {
                File.Delete(path);
            }
        });

    [Fact]
    public void A_Picture_Already_At_The_Size_A_Face_Is_Drawn_Is_Kept_As_It_Is() =>
        HeadlessUi.On(() => {
            var path = Path.Combine(Path.GetTempPath(), $"shiranuix-face-{Guid.NewGuid():N}.png");

            try {
                WritePicture(path, 64);

                var bytes = GraphAvatar.Read(path);

                Assert.NotNull(bytes);

                using var face = new Bitmap(new MemoryStream(bytes!));
                Assert.Equal(64, face.PixelSize.Width);
                Assert.Equal(64, face.PixelSize.Height);
            } finally {
                File.Delete(path);
            }
        });

    /// <summary>
    /// Writes a picture of the given size to the given path, the way a
    /// writer's own picture would be found on the machine.
    /// </summary>
    /// <param name="path">Where the picture is written.</param>
    /// <param name="size">How wide the picture is.</param>
    private static void WritePicture(string path, int size) {
        using var picture = new RenderTargetBitmap(new PixelSize(size, size));
        using (var context = picture.CreateDrawingContext()) {
            context.FillRectangle(Brushes.Red, new Rect(0, 0, size, size));
        }

        using var stream = File.Create(path);
        picture.Save(stream, new PngBitmapEncoderOptions());
    }
}
