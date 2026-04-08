using System.Drawing.Drawing2D;
using LiteMarkWin.Models;
using LiteMarkWin.Native;

namespace LiteMarkWin.UI;

internal sealed class OverlayForm : Form
{
    private IReadOnlyList<MarkupShape> _shapes = Array.Empty<MarkupShape>();
    private MarkupShape? _previewShape;
    private Color _drawingColor = Color.Red;
    private byte _alpha = 255;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Bounds = SystemInformation.VirtualScreen;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExTransparent;
            return cp;
        }
    }

    public void UpdateScene(IReadOnlyList<MarkupShape> shapes, MarkupShape? previewShape, Color drawingColor, byte alpha)
    {
        _shapes = shapes;
        _previewShape = previewShape;
        _drawingColor = drawingColor;
        _alpha = alpha;

        if (_shapes.Count == 0 && _previewShape is null)
        {
            Invalidate();
            Update();
            if (Visible)
            {
                Hide();
            }

            return;
        }

        if (!Visible)
        {
            Show();
        }

        Invalidate();
        Update();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.Magenta);
        using var brush = new SolidBrush(Color.FromArgb(_alpha, _drawingColor));

        foreach (var shape in _shapes)
        {
            DrawShape(e.Graphics, brush, shape);
        }

        if (_previewShape is not null)
        {
            DrawShape(e.Graphics, brush, _previewShape);
        }
    }

    private void DrawShape(Graphics graphics, Brush brush, MarkupShape shape)
    {
        var offsetX = -Bounds.Left;
        var offsetY = -Bounds.Top;

        if (shape.Mode == MarkupMode.Rectangle)
        {
            var left = Math.Min(shape.X1, shape.X2) + offsetX;
            var top = Math.Min(shape.Y1, shape.Y2) + offsetY;
            var width = Math.Max(shape.Thickness, Math.Abs(shape.X2 - shape.X1));
            var height = Math.Max(shape.Thickness, Math.Abs(shape.Y2 - shape.Y1));
            var t = shape.Thickness;

            graphics.FillRectangle(brush, left, top, width, t);
            graphics.FillRectangle(brush, left, top + height - t, width, t);
            graphics.FillRectangle(brush, left, top, t, height);
            graphics.FillRectangle(brush, left + width - t, top, t, height);
            return;
        }

        if (shape.Mode == MarkupMode.HorizontalLine)
        {
            var left = Math.Min(shape.X1, shape.X2) + offsetX;
            var top = shape.Y1 + offsetY;
            var width = Math.Max(shape.Thickness, Math.Abs(shape.X2 - shape.X1));
            graphics.FillRectangle(brush, left, top, width, shape.Thickness);
        }
    }
}
