namespace StruxureGuard.UI.Tools;

public sealed class ExcelRenamerToolForm : PlaceholderToolFormBase
{
    public ExcelRenamerToolForm() : base(
        "ExcelRenamer",
        "Rename Excel files based on cell B1 (port from Python Excelrenamer).",
        logTag: "excel-renamer")
    { }
}
