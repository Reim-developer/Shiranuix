namespace Narika.Tests.Fakes;

/// <summary>
/// An HTTP handler that answers whatever the test says it should, so the
/// services that reach out can be watched without reaching anywhere: one
/// lambda decides the answer, including one that never answers at all.
/// </summary>
public sealed class StubHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler {
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => send(request, cancellationToken);
}
