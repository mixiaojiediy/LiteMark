using System.Drawing;
using System.Drawing.Drawing2D;
using LiteMarkWin.Native;

namespace LiteMarkWin.Services;

internal static class TrayIconFactory
{
    public static Icon CreateEnabledIcon() => CreateIcon(
        ColorTranslator.FromHtml("#E94B35"),
        ColorTranslator.FromHtml("#FFF1EC"),
        ColorTranslator.FromHtml("#FFB84D"));

    public static Icon CreatePausedIcon() => CreateIcon(
        ColorTranslator.FromHtml("#8F8F95"),
        ColorTranslator.FromHtml("#F1F1F3"),
        ColorTranslator.FromHtml("#C6C7CC"));

    private static Icon CreateIcon(Color frameColor, Color panelColor, Color markerColor)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        graphics.ScaleTransform(2f, 2f);

        using var framePen = new Pen(frameColor, 3.2f)
        {
            LineJoin = LineJoin.Round
        };
        using var panelBrush = new SolidBrush(panelColor);
        using var markerBrush = new SolidBrush(markerColor);
        using var tipBrush = new SolidBrush(frameColor);
        using var guidePen = new Pen(frameColor, 2.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var panel = new RectangleF(3.8f, 4.8f, 19.8f, 16.8f);
        graphics.FillRectangle(panelBrush, panel);
        graphics.DrawRectangle(framePen, panel.X, panel.Y, panel.Width, panel.Height);

        graphics.DrawLine(guidePen, 7.4f, 10.4f, 20.8f, 10.4f);
        graphics.DrawLine(guidePen, 7.4f, 15.1f, 17.2f, 15.1f);

        var markerBody = new PointF[]
        {
            new(17.4f, 21.3f),
            new(23.4f, 15.3f),
            new(27.4f, 19.3f),
            new(21.4f, 25.3f)
        };
        graphics.FillPolygon(markerBrush, markerBody);
        graphics.DrawPolygon(framePen, markerBody);

        var markerTip = new PointF[]
        {
            new(15.7f, 22.9f),
            new(17.8f, 20.8f),
            new(19.6f, 24.6f)
        };
        graphics.FillPolygon(tipBrush, markerTip);

        graphics.DrawLine(guidePen, 12.9f, 27f, 20.5f, 23.8f);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
