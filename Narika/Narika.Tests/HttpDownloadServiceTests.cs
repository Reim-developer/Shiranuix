using System.Net;
using Narika.Services.Download;
using Narika.Services.Install;
using Narika.Tests.Fakes;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="HttpDownloadService"/>: the fetch that streams a
/// release's bytes to a folder of this application's own, watched entirely
/// through a body the test hands it — no network is ever asked anything.
/// </summary>
public sealed class HttpDownloadServiceTests : IDisposable {
    /// <summary>
    /// More than one buffer's worth, so the bar has somewhere to count to.
    /// </summary>
    private static readonly byte[] Payload =
        Enumerable.Range(0, 300_000).Select(i => (byte)(i % 251)).ToArray();

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N"));

    public HttpDownloadServiceTests() => Directory.CreateDirectory(_folder);

    /// <summary>
    /// The folder this test's fetch lands in, taken back whole.
    /// </summary>
    public void Dispose() {
        try {
            if (Directory.Exists(_folder)) {
                Directory.Delete(_folder, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    [Fact]
    public async Task Bytes_Are_Streamed_Landed_And_Counted_As_They_Arrive() {
        var service = new HttpDownloadService(
            new FakeReleaseCatalog(),
            "shiranuix",
            new HttpClient(new StubHttpHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Payload),
                }))),
            _folder);

        var notes = new List<InstallProgress>();
        var outcome = await service.DownloadAsync(
            new InlineProgress<InstallProgress>(notes.Add),
            CancellationToken.None);

        // The fetch put the release's own bytes down under the release's
        // own name, and remembers where they are for the install to come.
        Assert.Equal(Path.Combine(_folder, "Shiranuix.zip"), outcome.ArchivePath);
        Assert.Equal(outcome.ArchivePath, service.Landed!.ArchivePath);
        Assert.Equal(Payload, File.ReadAllBytes(outcome.ArchivePath));
        Assert.False(File.Exists(outcome.ArchivePath + ".part"));

        // The share landed was counted as it went, not merely at the end.
        Assert.Contains(notes, note => note.Percent == 100);
        Assert.Contains(notes, note => note.Percent is > 0 and < 100);
    }

    [Fact]
    public async Task A_Fetch_The_Place_Cannot_Serve_Lands_Nothing() {
        var service = new HttpDownloadService(
            new FakeReleaseCatalog(),
            "shiranuix",
            new HttpClient(new StubHttpHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
            _folder);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.DownloadAsync(
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None));

        // A fetch that did not land is not half-landed either: nothing
        // stays behind for a later walk to mistake for an archive.
        Assert.Null(service.Landed);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task A_Fetch_That_Stops_Mid_Way_Takes_Its_Part_With_It() {
        var service = new HttpDownloadService(
            new FakeReleaseCatalog(),
            "shiranuix",
            new HttpClient(new StubHttpHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StreamContent(new StallingBody()),
                }))),
            _folder);

        using var cancellation = new CancellationTokenSource();

        var running = service.DownloadAsync(
            new InlineProgress<InstallProgress>(_ => { }), cancellation.Token);
        cancellation.Cancel();

        // Whichever moment the stop was observed, the outcome is the same
        // shape: thrown out, nothing landed, nothing half-written left on
        // the disk to be unpacked by mistake another day.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Null(service.Landed);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    /// <summary>
    /// A body that hands over one buffer and then stands still until the
    /// fetch is cancelled — a line that stops, which is the one thing a
    /// body can do that a test cannot simply wait through.
    /// </summary>
    private sealed class StallingBody : Stream {
        private bool _handed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellation = default) {
            if (_handed) {
                await Task.Delay(Timeout.Infinite, cancellation);
            }

            _handed = true;
            buffer.Span.Fill(0x5A);
            return buffer.Length;
        }

        public override void Flush() {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
