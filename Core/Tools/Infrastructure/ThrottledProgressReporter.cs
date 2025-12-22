using System;

namespace StruxureGuard.Core.Tools.Infrastructure;

/// <summary>
/// Throttles progress reports by only forwarding the latest value at most once per interval.
/// Dispose-safe, race-safe, and supports Flush().
/// </summary>
public sealed class ThrottledProgressReporter<T> : IProgress<T>, IDisposable
{
    private readonly object _gate = new();

    private readonly IProgress<T> _inner;
    private readonly TimeSpan _interval;

    private System.Threading.Timer? _timer;

    private bool _disposed;

    private T? _latest;
    private bool _hasLatest;

    public ThrottledProgressReporter(IProgress<T> inner, TimeSpan interval)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _interval = interval;
    }

    public void Report(T value)
    {
        lock (_gate)
        {
            if (_disposed) return;

            _latest = value;
            _hasLatest = true;

            // Start timer lazily (one timer per reporter), then let it tick.
            if (_timer == null)
            {
                _timer = new System.Threading.Timer(_ => Tick(), null, _interval, _interval);
            }
        }
    }

    /// <summary>
    /// Immediately forwards the latest buffered progress (if any).
    /// </summary>
    public void Flush()
    {
        T? toSend = default;
        bool send;

        lock (_gate)
        {
            if (_disposed) return;

            send = _hasLatest;
            if (send)
            {
                toSend = _latest;
                _hasLatest = false;
            }
        }

        if (send && toSend is not null)
            _inner.Report(toSend);
    }

    private void Tick()
    {
        T? toSend = default;
        bool send;

        lock (_gate)
        {
            if (_disposed) return;

            send = _hasLatest;
            if (send)
            {
                toSend = _latest;
                _hasLatest = false;
            }
        }

        if (send && toSend is not null)
            _inner.Report(toSend);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            try { _timer?.Dispose(); } catch { /* ignore */ }
            _timer = null;

            _latest = default;
            _hasLatest = false;
        }
    }
}
