using System.Collections.Concurrent;

namespace StruxureGuard.Core.Logging;

public sealed class InMemoryLogSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();
    private readonly int _maxLines;

    public InMemoryLogSink(int maxLines = 5000)
    {
        _maxLines = Math.Max(100, maxLines);
    }

    public int Count => _events.Count;

    public void Write(LogEvent e)
    {
        _events.Enqueue(e);

        while (_events.Count > _maxLines && _events.TryDequeue(out _)) { }
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }

    public List<LogEvent> SnapshotEvents()
        => _events.ToArray().ToList();

    public List<LogEvent> SnapshotEventsFrom(int startIndex)
    {
        var arr = _events.ToArray();
        if (startIndex <= 0) return arr.ToList();
        if (startIndex >= arr.Length) return new List<LogEvent>();
        return arr.Skip(startIndex).ToList();
    }
}
