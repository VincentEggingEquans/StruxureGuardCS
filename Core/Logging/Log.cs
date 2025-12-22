using System.Threading;

namespace StruxureGuard.Core.Logging;

public static class Log
{
    private static InMemoryLogSink? _mem;
    private static FileLogSink? _file;
    private static string? _logFolder;

    private static long _seq;

    public static InMemoryLogSink Memory =>
        _mem ?? throw new InvalidOperationException("Log not initialized.");

    public static string LogFolder =>
        _logFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StruxureGuard",
            "Logs");

    public static string CurrentLogFilePath =>
        Path.Combine(LogFolder, $"struxureguard_{DateTime.Now:yyyy-MM-dd}.log");

    public static void Init(string logFolder, int memMax = 5000)
    {
        _logFolder = logFolder;

        _mem = new InMemoryLogSink(memMax);
        _file = new FileLogSink(logFolder);

        Info("bootstrap", "Logger initialized");
        Info("bootstrap", $"Log folder: {logFolder}");
    }

    public static void Trace(string cat, string msg) => Write(LogLevelEx.Trace, cat, msg);
    public static void Debug(string cat, string msg) => Write(LogLevelEx.Debug, cat, msg);
    public static void Info(string cat, string msg)  => Write(LogLevelEx.Info,  cat, msg);
    public static void Warn(string cat, string msg)  => Write(LogLevelEx.Warn,  cat, msg);
    public static void Error(string cat, string msg, Exception? ex = null) => Write(LogLevelEx.Error, cat, msg, ex);
    public static void Fatal(string cat, string msg, Exception? ex = null) => Write(LogLevelEx.Fatal, cat, msg, ex);

    private static void Write(LogLevelEx level, string category, string message, Exception? ex = null)
    {
        // fail-soft during very early startup
        if (_mem is null || _file is null) return;

        var seq = Interlocked.Increment(ref _seq);
        var tid = Environment.CurrentManagedThreadId;

        var ev = new LogEvent(
            Sequence: seq,
            Timestamp: DateTime.Now,
            Level: level,
            Category: category,
            Message: message,
            Exception: ex,
            ThreadId: tid);

        _mem.Write(ev);
        _file.Write(ev);
    }
}
