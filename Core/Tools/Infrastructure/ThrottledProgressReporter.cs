using System;
using System.Diagnostics;
using System.Threading;

namespace StruxureGuard.Core.Tools.Infrastructure;

/// <summary>
/// Wraps an IProgress&lt;T&gt; and forwards reports at most once per time window.
/// Coalesces to the latest value ("last write wins").
/// Thread-safe and UI-agnostic.
/// </summary>
public sealed class ThrottledProgressReporter<T> : IProgress<T>, IDisposable
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minInterval;

    private readonly object _lock = new();
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    private long _lastSentMs;
    private bool _hasPending;
    private T _pending = default!;

    // Explicit System.Threading.Timer (not WinForms Timer)
    private System.Threading.Timer _timer;

    private bool _disposed;

    public ThrottledProgressReporter(IProgress<T> inner, TimeSpan minInterval)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (minInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minInterval), "minInterval must be > 0.");

        _minInterval = minInterval;
        _lastSentMs = -1;
    }

    public void Report(T value)
    {
        if (_disposed) return;

        long now = _sw.ElapsedMilliseconds;

        lock (_lock)
        {
            _pending = value;
            _hasPending = true;

            // first send or enough time elapsed -> send immediately
            if (_lastSentMs < 0 || (now - _lastSentMs) >= (long)_minInterval.TotalMilliseconds)
            {
                SendPending_NoLock(now);
                return;
            }

            // otherwise schedule a flush (only one timer)
            if (_timer == null)
            {
                var due = (int)Math.Max(1, ((long)_minInterval.TotalMilliseconds - (now - _lastSentMs)));
                _timer = new System.Threading.Timer(_ => FlushFromTimer(), null, due, Timeout.Infinite);
            }
        }
    }

    private void FlushFromTimer()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            long now = _sw.ElapsedMilliseconds;
            long elapsedSinceLast = now - _lastSentMs;

            if (_lastSentMs < 0 || elapsedSinceLast >= (long)_minInterval.TotalMilliseconds)
            {
                SendPending_NoLock(now);
                DisposeTimer_NoLock();
                return;
            }

            // still too early (rare), reschedule
            var due = (int)Math.Max(1, ((long)_minInterval.TotalMilliseconds - elapsedSinceLast));
            _timer?.Change(due, Timeout.Infinite);
        }
    }

    private void SendPending_NoLock(long nowMs)
    {
        if (!_hasPending) return;

        var v = _pending;
        _hasPending = false;
        _pending = default!;

        _lastSentMs = nowMs;

        // forward (inner may marshal to UI thread if it's Progress<T>)
        _inner.Report(v);
    }

    private void DisposeTimer_NoLock()
    {
        try { _timer?.Dispose(); } catch { /* ignore */ }
        _timer = null;
    }

    /// <summary>
    /// Forces a final flush of the latest pending value (if any).
    /// </summary>
    public void Flush()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            long now = _sw.ElapsedMilliseconds;
            SendPending_NoLock(now);
            DisposeTimer_NoLock();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            // last update before we go away
            SendPending_NoLock(_sw.ElapsedMilliseconds);
            DisposeTimer_NoLock();
        }
    }
}
