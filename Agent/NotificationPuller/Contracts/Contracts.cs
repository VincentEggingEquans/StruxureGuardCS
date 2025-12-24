using System;

namespace StruxureGuard.NotificationPullerAgent
{
    public enum AgentStatus
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Stopped = 4,
        Error = 5
    }

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
    }

    public sealed class AgentStatusChangedEventArgs : EventArgs
    {
        public DateTime TimestampUtc { get; }
        public AgentStatus Status { get; }
        public string? Detail { get; }

        public AgentStatusChangedEventArgs(AgentStatus status, string? detail = null)
        {
            TimestampUtc = DateTime.UtcNow;
            Status = status;
            Detail = detail;
        }
    }

    public sealed class TargetProgressEventArgs : EventArgs
    {
        public DateTime TimestampUtc { get; }
        public string Target { get; }
        public int Index { get; }
        public int Total { get; }
        public string? Phase { get; }
        public string? Detail { get; }
        public bool IsSuccess { get; }
        public Exception? Exception { get; }

        public TargetProgressEventArgs(
            string target,
            int index,
            int total,
            string? phase = null,
            string? detail = null,
            bool isSuccess = false,
            Exception? exception = null)
        {
            TimestampUtc = DateTime.UtcNow;
            Target = target;
            Index = index;
            Total = total;
            Phase = phase;
            Detail = detail;
            IsSuccess = isSuccess;
            Exception = exception;
        }
    }
}
namespace StruxureGuard.NotificationPullerAgent.Contracts
{
    // Re-export types die al bestaan in StruxureGuard.NotificationPullerAgent (Host\AgentEvents.cs)
    // Hiermee blijven je bestaande `using ...Contracts;` statements werken.

    public enum AgentStatus : int
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Stopped = 4,
        Error = 5
    }

    public enum AgentLogLevel : int
    {
        Trace = 0,
        Debug = 1,
        Info  = 2,
        Warn  = 3,
        Error = 4
    }

    public sealed class AgentLogEventArgs : System.EventArgs
    {
        public System.DateTime TimestampUtc { get; }
        public AgentLogLevel Level { get; }
        public string Message { get; }
        public System.Exception? Exception { get; }

        public AgentLogEventArgs(AgentLogLevel level, string message, System.Exception? exception = null)
        {
            TimestampUtc = System.DateTime.UtcNow;
            Level = level;
            Message = message;
            Exception = exception;
        }
    }

    public sealed class AgentStatusChangedEventArgs : System.EventArgs
    {
        public System.DateTime TimestampUtc { get; }
        public AgentStatus Status { get; }
        public string? Detail { get; }

        public AgentStatusChangedEventArgs(AgentStatus status, string? detail = null)
        {
            TimestampUtc = System.DateTime.UtcNow;
            Status = status;
            Detail = detail;
        }
    }

    public sealed class TargetProgressEventArgs : System.EventArgs
    {
        public System.DateTime TimestampUtc { get; }
        public string Target { get; }
        public int Index { get; }
        public int Total { get; }
        public string? Phase { get; }
        public string? Detail { get; }
        public bool IsSuccess { get; }
        public System.Exception? Exception { get; }

        public TargetProgressEventArgs(
            string target,
            int index,
            int total,
            string? phase = null,
            string? detail = null,
            bool isSuccess = false,
            System.Exception? exception = null)
        {
            TimestampUtc = System.DateTime.UtcNow;
            Target = target;
            Index = index;
            Total = total;
            Phase = phase;
            Detail = detail;
            IsSuccess = isSuccess;
            Exception = exception;
        }
    }
}
