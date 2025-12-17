namespace StruxureGuard.Core.Logging;

public sealed record LogEvent(
    DateTime Timestamp,
    LogLevelEx Level,
    string Category,
    string Message,
    Exception? Exception = null
);
