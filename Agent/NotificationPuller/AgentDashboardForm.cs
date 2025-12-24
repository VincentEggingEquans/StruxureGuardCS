using System;
using System.Windows.Forms;

namespace StruxureGuard.NotificationPullerAgent
{
    public sealed class AgentDashboardForm : Form
    {
        private readonly PullerAgent _agent;

        private readonly Label _lblStatus = new() { AutoSize = true, Text = "Status: (starting...)" };
        private readonly Button _btnStart = new() { Text = "Start", Width = 80 };
        private readonly Button _btnStop  = new() { Text = "Stop", Width = 80, Left = 90 };
        private readonly ListBox _lstLog  = new() { Top = 40, Width = 760, Height = 420, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        public AgentDashboardForm(PullerAgent agent)
        {
            _agent = agent;

            Text = "StruxureGuard NotificationPuller Agent";
            Width = 800;
            Height = 520;

            Controls.Add(_lblStatus);
            Controls.Add(_btnStart);
            Controls.Add(_btnStop);
            Controls.Add(_lstLog);

            _btnStart.Top = 10;
            _btnStop.Top = 10;
            _btnStart.Left = 200;
            _btnStop.Left = 290;

            _lblStatus.Top = 14;
            _lblStatus.Left = 10;

            _agent.StatusChanged += s => BeginInvoke(() => _lblStatus.Text = $"Status: {s}");
            _agent.Log += msg => BeginInvoke(() =>
            {
                _lstLog.Items.Add(msg);
                _lstLog.TopIndex = _lstLog.Items.Count - 1;
            });

            _btnStart.Click += (_, __) => _agent.Start();
            _btnStop.Click += (_, __) => _agent.Stop();

            FormClosing += (_, __) => _agent.Stop();
        }
    }
}
