namespace ShopHub.Api.Observability;

public class TrafficMetricsMiddleware(RequestDelegate next, TrafficMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength is { } requestBytes)
        {
            metrics.RecordRequestBytes(requestBytes);
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        metrics.RecordVisitor(ipAddress, userAgent);

        // Content-Length isn't reliably set on the way out, so count actual bytes written
        // rather than trusting the header.
        var originalBody = context.Response.Body;
        await using var countingStream = new CountingStream(originalBody);
        context.Response.Body = countingStream;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
            metrics.RecordResponseBytes(countingStream.BytesWritten);
        }
    }
}

internal sealed class CountingStream(Stream inner) : Stream
{
    public long BytesWritten { get; private set; }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        BytesWritten += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer, offset, count, cancellationToken);
        BytesWritten += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        BytesWritten += buffer.Length;
    }
}
