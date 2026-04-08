using LiteMarkWin.Native;

namespace LiteMarkWin.UI;

internal sealed class InputBlockerForm : Form
{
    public event Action<Point>? PointerMoved;
    public event Action<Point>? PointerPressed;
    public event Action<Point>? PointerReleased;

    public InputBlockerForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.01d;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            return cp;
        }
    }

    public void ActivateBlocker()
    {
        if (!Visible)
        {
            Show();
        }
        else
        {
            BringToFront();
        }
    }

    public void DeactivateBlocker()
    {
        if (Visible)
        {
            Hide();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointerMoved?.Invoke(PointToScreen(e.Location));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            PointerPressed?.Invoke(PointToScreen(e.Location));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            PointerReleased?.Invoke(PointToScreen(e.Location));
        }
    }
}
