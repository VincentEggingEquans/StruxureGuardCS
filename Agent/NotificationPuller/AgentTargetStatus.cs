namespace StruxureGuard.NotificationPullerAgent;

public enum TargetState
{
    Idle,
    Connecting,
    Downloading,
    Ok,
    Error
}

public sealed class AgentTargetStatus
{
    public string Ip { get; set; } = "";
    public TargetState State { get; set; } = TargetState.Idle;
    public DateTime? LastSync { get; set; }
    public string Message { get; set; } = "";
    public int FilesDownloaded { get; set; }
}
