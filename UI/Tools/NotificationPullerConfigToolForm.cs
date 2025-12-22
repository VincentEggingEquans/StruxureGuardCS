namespace StruxureGuard.UI.Tools;

public sealed class NotificationPullerConfigToolForm : PlaceholderToolFormBase
{
    public NotificationPullerConfigToolForm() : base(
        "NotificationPuller Config",
        "Configuration UI for notification puller (port from Python NotificationpullerConfigGUI).",
        logTag: "notif-puller")
    { }
}
