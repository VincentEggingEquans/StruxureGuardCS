using System.Collections.Concurrent;
using System.Threading;

namespace StruxureGuard.Core.Logging;

public sealed class InMemoryLogSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();
    private readonly int _maxLines;

    private long _dropped;

    public InMemoryLogSink(int maxLines = 5000)
    {
        _maxLines = Math.Max(100, maxLines);
    }

    public int Count => _events.Count;
    public int MaxLines => _maxLines;
    public long DroppedCount => Interlocked.Read(ref _dropped);

    public void Write(LogEvent e)
    {
        _events.Enqueue(e);

        while (_events.Count > _maxLines && _events.TryDequeue(out _))
            Interlocked.Increment(ref _dropped);
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _dropped, 0);
    }

    public List<LogEvent> SnapshotEvents()
        => _events.ToArray().ToList();

    public List<LogEvent> SnapshotEventsSince(long lastSequence)
    {
        var arr = _events.ToArray();
        if (arr.Length == 0) return new List<LogEvent>();
        return arr.Where(e => e.Sequence > lastSequence).ToList();
    }
}
