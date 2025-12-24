// File: Agent/NotificationPuller/Engine/NotificationPullerEngine.cs
using System.Threading.Tasks;
using StruxureGuard.NotificationPullerAgent.Config;
using StruxureGuard.NotificationPullerAgent.Host;
using StruxureGuard.NotificationPullerAgent.Contracts;

namespace StruxureGuard.NotificationPullerAgent.Engine
{
    public sealed class NotificationPullerEngine
    {
        public event System.EventHandler<AgentLogEventArgs>? Log;
        public event EventHandler<TargetProgressEventArgs>? TargetProgress;

        public async Task RunOnceAsync(NotificationPullerAgentConfig cfg)
        {
            Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Info, $"RunOnce start. ips={cfg.Ips.Count}"));
            foreach (var ip in cfg.Ips)
            {
                TargetProgress?.Invoke(this, new TargetProgressEventArgs(ip, "connect", "Connecting (dummy)..."));
                await Task.Delay(30).ConfigureAwait(false);

                TargetProgress?.Invoke(this, new TargetProgressEventArgs(ip, "download", "Downloading (dummy)..."));
                await Task.Delay(30).ConfigureAwait(false);

                TargetProgress?.Invoke(this, new TargetProgressEventArgs(ip, "done", "Done (dummy)."));
            }
            Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Info, "RunOnce finished."));
        }
    }
}
