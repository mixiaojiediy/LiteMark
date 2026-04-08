using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace LiteMarkWin.Models;

internal sealed class HotkeyGesture
{
    public bool Control { get; init; }

    public bool Alt { get; init; }

    public bool Shift { get; init; }

    public bool Win { get; init; }

    public Keys Key { get; init; }

    [JsonIgnore]
    public bool IsEmpty => Key == Keys.None;

    public bool IsPressed(IReadOnlySet<Keys> pressedKeys)
    {
        if (Key == Keys.None)
        {
            return false;
        }

        if (Control && !pressedKeys.Contains(Keys.ControlKey))
        {
            return false;
        }

        if (Alt && !pressedKeys.Contains(Keys.Menu))
        {
            return false;
        }

        if (Shift && !pressedKeys.Contains(Keys.ShiftKey))
        {
            return false;
        }

        if (Win && !pressedKeys.Contains(Keys.LWin) && !pressedKeys.Contains(Keys.RWin))
        {
            return false;
        }

        return pressedKeys.Contains(Key);
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        if (Key != Keys.None)
        {
            parts.Add(KeyToText(Key));
        }

        return string.Join("+", parts);
    }

    public static HotkeyGesture DefaultRectangle() => new()
    {
        Alt = true,
        Key = Keys.W
    };

    public static HotkeyGesture DefaultHorizontalLine() => new()
    {
        Alt = true,
        Key = Keys.E
    };

    public static bool TryCreate(Keys keyData, out HotkeyGesture gesture)
    {
        var baseKey = keyData & Keys.KeyCode;
        var control = keyData.HasFlag(Keys.Control);
        var alt = keyData.HasFlag(Keys.Alt);
        var shift = keyData.HasFlag(Keys.Shift);
        var win = keyData.HasFlag(Keys.LWin) || keyData.HasFlag(Keys.RWin);

        if (!control && !alt && !shift && !win)
        {
            gesture = new HotkeyGesture();
            return false;
        }

        if (IsModifier(baseKey))
        {
            gesture = new HotkeyGesture();
            return false;
        }

        gesture = new HotkeyGesture
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            Key = baseKey
        };

        return true;
    }

    public static HotkeyGesture ParseOrDefault(string? value, HotkeyGesture fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var control = false;
        var alt = false;
        var shift = false;
        var win = false;
        var key = Keys.None;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    control = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                    win = true;
                    break;
                default:
                    if (!Enum.TryParse(part, true, out key))
                    {
                        return fallback;
                    }
                    break;
            }
        }

        if (key == Keys.None)
        {
            return fallback;
        }

        return new HotkeyGesture
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            Key = key
        };
    }

    public string Serialize() => ToString();

    private static bool IsModifier(Keys key) =>
        key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin;

    private static string KeyToText(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return ((char)('0' + (key - Keys.D0))).ToString();
        }

        return key switch
        {
            Keys.Space => "Space",
            Keys.Tab => "Tab",
            _ => key.ToString()
        };
    }
}
