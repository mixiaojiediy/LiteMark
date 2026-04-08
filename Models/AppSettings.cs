using System.Drawing;

namespace LiteMarkWin.Models;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;

    public string RectangleHotkey { get; set; } = HotkeyGesture.DefaultRectangle().Serialize();

    public string LineHotkey { get; set; } = HotkeyGesture.DefaultHorizontalLine().Serialize();

    public string LineColor { get; set; } = "FF3B30";

    public int LineWidth { get; set; } = 3;

    public HotkeyGesture GetRectangleGesture() =>
        HotkeyGesture.ParseOrDefault(RectangleHotkey, HotkeyGesture.DefaultRectangle());

    public HotkeyGesture GetLineGesture() =>
        HotkeyGesture.ParseOrDefault(LineHotkey, HotkeyGesture.DefaultHorizontalLine());

    public Color GetDrawingColor()
    {
        var normalized = (LineColor ?? "FF3B30").Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "FF3B30";
        }

        return ColorTranslator.FromHtml($"#{normalized}");
    }

    public int GetLineWidth() => Math.Clamp(LineWidth, 1, 12);
}
