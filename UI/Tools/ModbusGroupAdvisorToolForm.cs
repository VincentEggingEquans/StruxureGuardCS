namespace StruxureGuard.UI.Tools;

public sealed class ModbusGroupAdvisorToolForm : ToolFormBase
{
    public ModbusGroupAdvisorToolForm() : base(
        "Modbus Group Advisor",
        "Advisor UI for Modbus group sizing/config (port from Python modbus_group_advisor).",
        logTag: "modbus-advisor")
    { }
}
