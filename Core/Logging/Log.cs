namespace StruxureGuard.Core.Logging;

public static class Log
{
    private static InMemoryLogSink? _mem;
    private static FileLogSink? _file;

    public static InMemoryLogSink Memory =>
        _mem ?? throw new InvalidOperationException("Log not initialized.");

    public static void Init(string logFolder, int memMax = 5000)
    {
        _mem = new InMemoryLogSink(memMax);
        _file = new FileLogSink(logFolder);

        Info("bootstrap", "Logger initialized");
        Info("bootstrap", $"Log folder: {logFolder}");
    }

    public static void Trace(string cat, string msg) => Write(LogLevelEx.Trace, cat, msg);
    public static void Debug(string cat, string msg) => Write(LogLevelEx.Debug, cat, msg);
    public static void Info(string cat, string msg)  => Write(LogLevelEx.Info, cat, msg);
    public static void Warn(string cat, string msg)  => Write(LogLevelEx.Warn, cat, msg);
    public static void Error(string cat, string msg, Exception? ex = null) => Write(LogLevelEx.Error, cat, msg, ex);
    public static void Fatal(string cat, string msg, Exception? ex = null) => Write(LogLevelEx.Fatal, cat, msg, ex);

    private static void Write(LogLevelEx level, string category, string message, Exception? ex = null)
    {
        if (_mem is null || _file is null) return;

        var ev = new LogEvent(DateTime.Now, level, category, message, ex);

        _mem.Write(ev);     // ✅ fixed
        _file.Write(ev);
    }
}
