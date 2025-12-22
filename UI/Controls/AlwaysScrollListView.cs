using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StruxureGuard.UI.Controls;

public class AlwaysScrollListView : ListView
{
    private const int SB_VERT = 1;

    private const int WM_SIZE = 0x0005;
    private const int WM_PAINT = 0x000F;
    private const int WM_WINDOWPOSCHANGED = 0x0047;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public AlwaysScrollListView()
    {
        Scrollable = true;
        HideSelection = false;
        FullRowSelect = true;
        View = View.Details;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Ensure modern themed scrollbars (matches WinForms/Explorer style better)
        try
        {
            _ = SetWindowTheme(Handle, "Explorer", null);
        }
        catch { /* visual-only */ }

        ForceVScroll();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) ForceVScroll();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ForceVScroll();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg is WM_SIZE or WM_PAINT or WM_WINDOWPOSCHANGED)
            ForceVScroll();
    }

    private void ForceVScroll()
    {
        if (!IsHandleCreated) return;

        try
        {
            // Force show vertical scrollbar even when there is no overflow
            ShowScrollBar(Handle, SB_VERT, true);
        }
        catch { /* visual-only */ }
    }
}
