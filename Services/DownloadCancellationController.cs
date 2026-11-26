using System;
using System.Threading;

namespace Webtoon_Downloader.Services;

public enum DownloadStopReason
{
    None,
    User,
    AuthenticationFailed
}

public sealed class DownloadCancellationController : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private int _reason;

    public CancellationToken Token => _source.Token;

    public bool IsCancellationRequested =>
        _source.IsCancellationRequested;

    public DownloadStopReason Reason =>
        (DownloadStopReason)Volatile.Read(ref _reason);

    public void Cancel(
        DownloadStopReason reason = DownloadStopReason.User)
    {
        if (Interlocked.CompareExchange(
                ref _reason,
                (int)reason,
                (int)DownloadStopReason.None) !=
            (int)DownloadStopReason.None)
            return;

        _source.Cancel();
    }

    public void Dispose()
    {
        _source.Dispose();
    }
}