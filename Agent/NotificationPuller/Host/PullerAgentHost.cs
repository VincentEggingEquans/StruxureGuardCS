// File: Agent/NotificationPuller/Host/PullerAgentHost.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using StruxureGuard.NotificationPullerAgent.Config;
using StruxureGuard.NotificationPullerAgent.Engine;

namespace StruxureGuard.NotificationPullerAgent.Host
{
    public sealed class PullerAgentHost : IDisposable
    {
        private readonly string _configPath;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly object _gate = new();

        private AgentStatus _status = AgentStatus.Stopped;
        private NotificationPullerAgentConfig? _config;

        private readonly NotificationPullerEngine _engine = new NotificationPullerEngine();

        public PullerAgentHost(string configPath)
        {
            _configPath = configPath;

            _engine.Log += (_, e) => Log?.Invoke(this, e);
            _engine.TargetProgress += (_, e) => TargetProgress?.Invoke(this, e);
        }

        public event EventHandler<AgentLogEventArgs>? Log;
        public event EventHandler<AgentStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<TargetProgressEventArgs>? TargetProgress;

        public AgentStatus Status => _status;
        public string ConfigPath => _configPath;

        public Task StartAsync()
        {
            lock (_gate)
            {
                if (_status is AgentStatus.Running or AgentStatus.Starting) return Task.CompletedTask;

                SetStatus(AgentStatus.Starting);
                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => LoopAsync(_cts.Token));
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            Task? loop = null;
            lock (_gate)
            {
                if (_status is AgentStatus.Stopped or AgentStatus.Stopping) return;
                SetStatus(AgentStatus.Stopping);
                _cts?.Cancel();
                loop = _loopTask;
            }

            if (loop != null)
            {
                try { await loop.ConfigureAwait(false); } catch { }
            }

            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
                SetStatus(AgentStatus.Stopped);
            }
        }

        public async Task RunOnceAsync()
        {
            if (_config == null) _config = NotificationPullerAgentConfigLoader.Load(_configPath);
            await _engine.RunOnceAsync(_config).ConfigureAwait(false);
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            try
            {
                _config = NotificationPullerAgentConfigLoader.Load(_configPath);
                Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Info, $"Config loaded. ips={_config.Ips.Count} path='{_configPath}'"));
                SetStatus(AgentStatus.Running);

                while (!ct.IsCancellationRequested)
                {
                    await _engine.RunOnceAsync(_config).ConfigureAwait(false);
                    var ms = Math.Max(1, _config.PollIntervalSeconds) * 1000;
                    Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Debug, $"Sleep {ms}ms"));
                    await Task.Delay(ms, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Info, "Loop canceled."));
            }
            catch (Exception ex)
            {
                Log?.Invoke(this, new AgentLogEventArgs(AgentLogLevel.Error, $"Loop crashed: {ex}"));
                SetStatus(AgentStatus.Faulted);
            }
        }

        private void SetStatus(AgentStatus newStatus)
        {
            var old = _status;
            _status = newStatus;
            StatusChanged?.Invoke(this, new AgentStatusChangedEventArgs(old, newStatus));
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
        }
    }
}
