using System.Drawing;
using LiteMarkWin.Models;

namespace LiteMarkWin.UI;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _rectangleHotkeyTextBox;
    private readonly TextBox _lineHotkeyTextBox;
    private readonly TextBox _colorTextBox;
    private readonly NumericUpDown _lineWidthUpDown;
    private readonly Icon _windowIcon;
    private CaptureTarget _captureTarget = CaptureTarget.None;

    private HotkeyGesture _rectangleGesture;
    private HotkeyGesture _lineGesture;

    public SettingsForm(AppSettings settings, Icon windowIcon)
    {
        _windowIcon = (Icon)windowIcon.Clone();

        Text = "LiteMark 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        Icon = _windowIcon;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        Padding = new Padding(12);

        _rectangleGesture = settings.GetRectangleGesture();
        _lineGesture = settings.GetLineGesture();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "矩形快捷键", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _rectangleHotkeyTextBox = new TextBox { ReadOnly = true, Text = _rectangleGesture.ToString(), Dock = DockStyle.Fill };
        layout.Controls.Add(_rectangleHotkeyTextBox, 1, 0);
        var rectangleButton = new Button { Text = "录制", Dock = DockStyle.Fill };
        rectangleButton.Click += (_, _) => BeginCapture(CaptureTarget.Rectangle);
        layout.Controls.Add(rectangleButton, 2, 0);

        layout.Controls.Add(new Label { Text = "横线快捷键", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _lineHotkeyTextBox = new TextBox { ReadOnly = true, Text = _lineGesture.ToString(), Dock = DockStyle.Fill };
        layout.Controls.Add(_lineHotkeyTextBox, 1, 1);
        var lineButton = new Button { Text = "录制", Dock = DockStyle.Fill };
        lineButton.Click += (_, _) => BeginCapture(CaptureTarget.Line);
        layout.Controls.Add(lineButton, 2, 1);

        layout.Controls.Add(new Label { Text = "线条颜色", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _colorTextBox = new TextBox { Text = $"#{settings.LineColor.TrimStart('#')}", Dock = DockStyle.Fill };
        layout.Controls.Add(_colorTextBox, 1, 2);
        layout.Controls.Add(new Label { Text = "#RRGGBB", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 2);

        layout.Controls.Add(new Label { Text = "线宽", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        _lineWidthUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 12,
            Value = settings.GetLineWidth(),
            Dock = DockStyle.Left,
            Width = 100
        };
        layout.Controls.Add(_lineWidthUpDown, 1, 3);
        layout.Controls.Add(new Label { Text = "1 - 12", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 3);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button { Text = "取消", AutoSize = true };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 3);

        var tips = new Label
        {
            AutoSize = true,
            Text = "提示：录制时请按带修饰键的组合键，例如 Alt+W 或 Ctrl+Shift+E。",
            Margin = new Padding(0, 12, 0, 0)
        };
        layout.Controls.Add(tips, 0, 5);
        layout.SetColumnSpan(tips, 3);
    }

    public AppSettings? UpdatedSettings { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _windowIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_captureTarget != CaptureTarget.None)
        {
            if (HotkeyGesture.TryCreate(keyData, out var gesture))
            {
                ApplyCapturedGesture(gesture);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BeginCapture(CaptureTarget target)
    {
        _captureTarget = target;

        if (target == CaptureTarget.Rectangle)
        {
            _rectangleHotkeyTextBox.Text = "请按组合键...";
        }
        else if (target == CaptureTarget.Line)
        {
            _lineHotkeyTextBox.Text = "请按组合键...";
        }
    }

    private void ApplyCapturedGesture(HotkeyGesture gesture)
    {
        if (_captureTarget == CaptureTarget.Rectangle)
        {
            _rectangleGesture = gesture;
            _rectangleHotkeyTextBox.Text = gesture.ToString();
        }
        else if (_captureTarget == CaptureTarget.Line)
        {
            _lineGesture = gesture;
            _lineHotkeyTextBox.Text = gesture.ToString();
        }

        _captureTarget = CaptureTarget.None;
    }

    private void SaveAndClose()
    {
        var normalizedColor = _colorTextBox.Text.Trim().TrimStart('#');
        if (normalizedColor.Length != 6 || normalizedColor.Any(ch => !Uri.IsHexDigit(ch)))
        {
            MessageBox.Show("颜色格式不正确，请使用 #RRGGBB。", "LiteMark", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UpdatedSettings = new AppSettings
        {
            Enabled = true,
            RectangleHotkey = _rectangleGesture.Serialize(),
            LineHotkey = _lineGesture.Serialize(),
            LineColor = normalizedColor.ToUpperInvariant(),
            LineWidth = (int)_lineWidthUpDown.Value
        };

        DialogResult = DialogResult.OK;
    }

    private enum CaptureTarget
    {
        None = 0,
        Rectangle = 1,
        Line = 2
    }
}
