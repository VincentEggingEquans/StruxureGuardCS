namespace StruxureGuard.Core.Logging;

public sealed record LogEvent(
    long Sequence,
    DateTime Timestamp,
    LogLevelEx Level,
    string Category,
    string Message,
    Exception? Exception = null,
    int ThreadId = 0
);
