using System;
using System.Linq;
using System.Windows.Forms;

namespace StruxureGuard.NotificationPullerAgent
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool noGui = args.Any(a => a.Equals("--nogui", StringComparison.OrdinalIgnoreCase));

            // TODO: maak jouw puller/service instance aan
            var agent = new PullerAgent(); // jouw bestaande logic wrapper

            if (noGui)
            {
                agent.RunHeadless(); // blocking loop / hosted service / whatever je nu doet
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new AgentDashboardForm(agent));
        }
    }
}
