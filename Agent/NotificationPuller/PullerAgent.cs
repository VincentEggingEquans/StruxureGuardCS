using System;
using System.Threading;
using System.Threading.Tasks;

namespace StruxureGuard.NotificationPullerAgent
{
    public sealed class PullerAgent : IDisposable
    {
        private readonly string _configPath;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        public event EventHandler<AgentStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<AgentLogEventArgs>? Log;

        public AgentState State { get; private set; } = AgentState.Idle;

        public PullerAgent(string configPath)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        }

        public void Start()
        {
            if (_runTask != null && !_runTask.IsCompleted)
            {
                RaiseLog(AgentLogLevel.Warn, "Start() called but agent is already running.");
                return;
            }

            RaiseStatus(AgentState.Starting, $"ConfigPath='{_configPath}'");
            _cts = new CancellationTokenSource();

            _runTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            if (_cts == null)
            {
                RaiseLog(AgentLogLevel.Warn, "Stop() called but agent was not running.");
                return;
            }

            RaiseStatus(AgentState.Stopping, "Cancellation requested.");
            _cts.Cancel();
        }

        public static int RunHeadless(string configPath)
        {
            using var agent = new PullerAgent(configPath);

            _agent.StatusChanged += (_, e) => AppendStatus(e);
            _agent.Log += (_, e) => AppendLog(e);

            agent.Start();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                agent.Stop();
            };

            // Wait until stopped
            while (agent.State == AgentState.Starting || agent.State == AgentState.Running || agent.State == AgentState.Stopping)
                Thread.Sleep(200);

            return agent.State == AgentState.Error ? 1 : 0;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                State = AgentState.Running;
                RaiseStatus(State, "Agent loop started.");

                // TODO: hier komt later:
                // - config laden uit JSON
                // - SSH connect
                // - notifications pullen
                // - export/zip/cleanup
                //
                // Voor nu: dummy loop zodat GUI iets kan tonen.
                var i = 0;
                while (!ct.IsCancellationRequested)
                {
                    i++;
                    RaiseLog(AgentLogLevel.Info, $"Heartbeat {i} (demo).");
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }

                State = AgentState.Stopped;
                RaiseStatus(State, "Agent loop stopped.");
            }
            catch (OperationCanceledException)
            {
                State = AgentState.Stopped;
                RaiseStatus(State, "Stopped (canceled).");
            }
            catch (Exception ex)
            {
                State = AgentState.Error;
                RaiseStatus(State, "Agent crashed.");
                RaiseLog(AgentLogLevel.Error, "Unhandled exception in agent loop.", ex);
            }
        }

        private void RaiseLog(AgentLogLevel level, string message, Exception? ex = null)
            => Log?.Invoke(this, new AgentLogEventArgs(level, message, ex));

        private void RaiseStatus(AgentState state, string? detail = null)
        {
            State = state;
            StatusChanged?.Invoke(this, new AgentStatusChangedEventArgs(state, detail));
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _cts?.Dispose(); } catch { /* ignore */ }
        }
    }
}
