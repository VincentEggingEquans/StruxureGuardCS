using System;
using StruxureGuard.NotificationPullerAgent.Contracts;

namespace StruxureGuard.NotificationPullerAgent
{
    public enum AgentLogLevel
    {
        Trace = 0,
        Debug = 1,
        Info  = 2,
        Warn  = 3,
        Error = 4
    }

    public sealed class AgentLogEventArgs : EventArgs
    {
        public DateTime TimestampUtc { get; }
        public AgentLogLevel Level { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        public AgentLogEventArgs(AgentLogLevel level, string message, Exception? exception = null)
        {
            TimestampUtc = DateTime.UtcNow;
            Level = level;
            Message = message;
            Exception = exception;
        }

        public override string ToString()
            => $"{TimestampUtc:O} [{Level}] {Message}" + (Exception != null ? $" | {Exception.GetType().Name}: {Exception.Message}" : "");
    }

    public enum AgentState
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Stopped = 4,
        Error = 5
    }

    public sealed class AgentStatusChangedEventArgs : EventArgs
    {
        public DateTime TimestampUtc { get; }
        public AgentState State { get; }
        public string? Detail { get; }

        public AgentStatusChangedEventArgs(AgentState state, string? detail = null)
        {
            TimestampUtc = DateTime.UtcNow;
            State = state;
            Detail = detail;
        }

        public override string ToString()
            => $"{TimestampUtc:O} State={State}" + (string.IsNullOrWhiteSpace(Detail) ? "" : $" | {Detail}");
    }
}
