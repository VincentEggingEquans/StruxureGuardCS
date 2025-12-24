// File: Agent/NotificationPuller/Config/NotificationPullerAgentConfig.cs
using System.Collections.Generic;

namespace StruxureGuard.NotificationPullerAgent.Config
{
    public sealed class NotificationPullerAgentConfig
    {
        public string ExportRoot { get; set; } = "";
        public string RemoteNotificationsDir { get; set; } = "/var/sbo/db/notifications";
        public string Pattern { get; set; } = "*";
        public string SaveMode { get; set; } = "all";

        public string IpsRaw { get; set; } = "";
        public List<string> Ips { get; set; } = new();

        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password1 { get; set; } = "";
        public string Password2 { get; set; } = "";
        public string Password3 { get; set; } = "";

        public bool MakeZip { get; set; } = true;

        // Agent default
        public int PollIntervalSeconds { get; set; } = 60;
    }
}
