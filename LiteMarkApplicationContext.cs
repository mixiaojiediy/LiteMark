using System.Drawing;
using LiteMarkWin.Input;
using LiteMarkWin.Models;
using LiteMarkWin.Native;
using LiteMarkWin.Services;
using LiteMarkWin.UI;

namespace LiteMarkWin;

internal sealed class LiteMarkApplicationContext : ApplicationContext
{
    private readonly AppSettingsStore _settingsStore = new();
    private readonly StartupManager _startupManager = new();
    private readonly OverlayForm _overlayForm = new();
    private readonly InputBlockerForm _inputBlocker = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly Icon _enabledIcon;
    private readonly Icon _pausedIcon;
    private readonly GlobalKeyboardHook _keyboardHook;
    private readonly GlobalMouseHook _mouseHook;
    private readonly System.Windows.Forms.Timer _stateTimer;
    private readonly System.Windows.Forms.Timer _drawTimer;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly HashSet<Keys> _pressedKeys = [];
    private readonly List<MarkupShape> _committedShapes = [];

    private AppSettings _settings;
    private HotkeyGesture _rectangleGesture;
    private HotkeyGesture _lineGesture;
    private MarkupMode _activeMode = MarkupMode.None;
    private MarkupShape? _previewShape;
    private bool _drawing;
    private bool _rectPressed;
    private bool _linePressed;
    private bool _rectPrimaryKeyPendingRelease;
    private bool _linePrimaryKeyPendingRelease;
    private byte _fadeAlpha = 255;
    private long _lastKeyboardEventTick;
    private Point _lastMousePoint;

    public LiteMarkApplicationContext()
    {
        DebugLogger.Reset();
        _settings = _settingsStore.Load();
        _rectangleGesture = _settings.GetRectangleGesture();
        _lineGesture = _settings.GetLineGesture();
        DebugLogger.Log($"app start enabled={_settings.Enabled} rect={_rectangleGesture} line={_lineGesture}");

        _enabledIcon = TrayIconFactory.CreateEnabledIcon();
        _pausedIcon = TrayIconFactory.CreatePausedIcon();

        _startupMenuItem = new ToolStripMenuItem("开机启动");
        _startupMenuItem.Click += (_, _) => ToggleStartup();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("设置", null, (_, _) => OpenSettings());
        contextMenu.Items.Add(_startupMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "LiteMark",
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.MouseClick += NotifyIconOnMouseClick;

        _keyboardHook = new GlobalKeyboardHook(HandleKeyboardEvent);

        _mouseHook = new GlobalMouseHook(HandleMouseHook);
        _inputBlocker.PointerMoved += HandleBlockedPointerMoved;
        _inputBlocker.PointerPressed += HandleBlockedPointerPressed;
        _inputBlocker.PointerReleased += HandleBlockedPointerReleased;

        _stateTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _stateTimer.Tick += (_, _) => MonitorHotkeyState();
        _stateTimer.Start();

        _drawTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _drawTimer.Tick += (_, _) => UpdatePreview();

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += (_, _) => FadeTick();
        _lastKeyboardEventTick = Environment.TickCount64;
        _lastMousePoint = Cursor.Position;

        UpdateStartupMenuState();
        UpdateTrayState();
    }

    protected override void ExitThreadCore()
    {
        DebugLogger.Log("app exit");
        _stateTimer.Stop();
        _drawTimer.Stop();
        _fadeTimer.Stop();
        _keyboardHook.Dispose();
        _mouseHook.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _enabledIcon.Dispose();
        _pausedIcon.Dispose();
        _inputBlocker.Close();
        _inputBlocker.Dispose();
        _overlayForm.Close();
        _overlayForm.Dispose();
        base.ExitThreadCore();
    }

    private void NotifyIconOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleEnabled();
        }
    }

    private bool HandleKeyboardEvent(KeyStateChangedEventArgs e)
    {
        var wasRectPressed = _rectPressed;
        var wasLinePressed = _linePressed;
        _lastKeyboardEventTick = Environment.TickCount64;

        if (e.IsDown)
        {
            _pressedKeys.Add(e.Key);
        }
        else
        {
            _pressedKeys.Remove(e.Key);
        }

        UpdatePrimaryKeyCaptureState(e);

        DebugLogger.Log(
            $"kbd key={e.Key} isDown={e.IsDown} beforeActive={_activeMode} beforeRect={wasRectPressed} beforeLine={wasLinePressed} keys={FormatPressedKeys()}");
        RefreshGestureState();

        if (e.IsDown && e.Key == Keys.Escape && _activeMode != MarkupMode.None)
        {
            DebugLogger.Log("kbd esc cancel active session");
            CancelActiveSession();
            return true;
        }

        var suppress = ShouldSuppressKeyboardEvent(e.Key, e.IsDown, wasRectPressed, wasLinePressed);
        DebugLogger.Log(
            $"kbd result key={e.Key} suppress={suppress} active={_activeMode} rect={_rectPressed} line={_linePressed} keys={FormatPressedKeys()}");
        return suppress;
    }

    private bool ShouldSuppressKeyboardEvent(Keys key, bool isDown, bool wasRectPressed, bool wasLinePressed)
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        if (key == _rectangleGesture.Key && _rectPrimaryKeyPendingRelease)
        {
            return true;
        }

        if (key == _lineGesture.Key && _linePrimaryKeyPendingRelease)
        {
            return true;
        }

        if (!isDown && IsModifierKey(key) && _activeMode == MarkupMode.None && !_rectPressed && !_linePressed)
        {
            return false;
        }

        if (IsGestureRelatedKey(_rectangleGesture, key) &&
            (wasRectPressed || _rectPressed || _activeMode == MarkupMode.Rectangle || _rectangleGesture.IsPressed(_pressedKeys)))
        {
            return true;
        }

        if (IsGestureRelatedKey(_lineGesture, key) &&
            (wasLinePressed || _linePressed || _activeMode == MarkupMode.HorizontalLine || _lineGesture.IsPressed(_pressedKeys)))
        {
            return true;
        }

        return false;
    }

    private static bool IsModifierKey(Keys key) =>
        key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin;

    private static bool IsGestureRelatedKey(HotkeyGesture gesture, Keys key)
    {
        if (gesture.Key == key)
        {
            return true;
        }

        if (gesture.Control && key == Keys.ControlKey)
        {
            return true;
        }

        if (gesture.Alt && key == Keys.Menu)
        {
            return true;
        }

        if (gesture.Shift && key == Keys.ShiftKey)
        {
            return true;
        }

        if (gesture.Win && (key == Keys.LWin || key == Keys.RWin))
        {
            return true;
        }

        return false;
    }

    private void EvaluateHotkeys()
    {
        var rectangleTriggered = _settings.Enabled && IsGestureTriggered(_rectangleGesture);
        var lineTriggered = _settings.Enabled && IsGestureTriggered(_lineGesture);
        var rectangleHeld = _settings.Enabled &&
            (_activeMode == MarkupMode.Rectangle ? IsGestureSessionHeld(_rectangleGesture) : rectangleTriggered);
        var lineHeld = _settings.Enabled &&
            (_activeMode == MarkupMode.HorizontalLine ? IsGestureSessionHeld(_lineGesture) : lineTriggered);

        DebugLogger.Log(
            $"eval active={_activeMode} rectPressed={_rectPressed} linePressed={_linePressed} rectTriggered={rectangleTriggered} rectHeld={rectangleHeld} lineTriggered={lineTriggered} lineHeld={lineHeld} keys={FormatPressedKeys()}");

        if (!_rectPressed && rectangleTriggered)
        {
            DebugLogger.Log("eval activate rectangle");
            ActivateMode(MarkupMode.Rectangle);
        }

        if (!_linePressed && lineTriggered)
        {
            DebugLogger.Log("eval activate line");
            ActivateMode(MarkupMode.HorizontalLine);
        }

        if (_rectPressed && !rectangleHeld && _activeMode == MarkupMode.Rectangle)
        {
            DebugLogger.Log("eval finish rectangle");
            FinishMode();
        }

        if (_linePressed && !lineHeld && _activeMode == MarkupMode.HorizontalLine)
        {
            DebugLogger.Log("eval finish line");
            FinishMode();
        }

        _rectPressed = rectangleHeld;
        _linePressed = lineHeld;
    }

    private void RefreshGestureState()
    {
        EvaluateHotkeys();
    }

    private void MonitorHotkeyState()
    {
        if (!_settings.Enabled)
        {
            return;
        }

        if (_activeMode == MarkupMode.None && !_rectPressed && !_linePressed)
        {
            return;
        }

        if (!ShouldRunStaleKeyCleanup())
        {
            return;
        }

        var changed = ClearReleasedGestureKeys(_rectangleGesture);
        changed |= ClearReleasedGestureKeys(_lineGesture);
        if (changed)
        {
            DebugLogger.Log($"watchdog cleanup changed keys={FormatPressedKeys()}");
            EvaluateHotkeys();
        }
    }

    private static bool IsKeyDown(Keys key)
    {
        if (key == Keys.ControlKey)
        {
            return (NativeMethods.GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.RControlKey) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
        }

        if (key == Keys.Menu)
        {
            return (NativeMethods.GetAsyncKeyState((int)Keys.LMenu) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.RMenu) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0;
        }

        if (key == Keys.ShiftKey)
        {
            return (NativeMethods.GetAsyncKeyState((int)Keys.LShiftKey) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.RShiftKey) & 0x8000) != 0
                || (NativeMethods.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0;
        }

        return (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    private bool ClearReleasedGestureKeys(HotkeyGesture gesture)
    {
        var changed = false;

        changed |= ClearKeyIfReleased(gesture.Key);

        if (gesture.Control)
        {
            changed |= ClearKeyIfReleased(Keys.ControlKey);
        }

        if (gesture.Alt)
        {
            changed |= ClearKeyIfReleased(Keys.Menu);
        }

        if (gesture.Shift)
        {
            changed |= ClearKeyIfReleased(Keys.ShiftKey);
        }

        if (gesture.Win)
        {
            changed |= ClearKeyIfReleased(Keys.LWin);
            changed |= ClearKeyIfReleased(Keys.RWin);
        }

        return changed;
    }

    private bool ClearKeyIfReleased(Keys key)
    {
        if (!_pressedKeys.Contains(key))
        {
            return false;
        }

        if (IsKeyDown(key))
        {
            return false;
        }

        _pressedKeys.Remove(key);
        DebugLogger.Log($"watchdog removed stale key={key}");
        return true;
    }

    private bool IsGestureTriggered(HotkeyGesture gesture) =>
        !gesture.IsEmpty && gesture.IsPressed(_pressedKeys);

    private bool IsGestureSessionHeld(HotkeyGesture gesture)
    {
        if (gesture.IsEmpty)
        {
            return false;
        }

        var hasModifier = false;

        if (gesture.Control)
        {
            hasModifier = true;
            if (!_pressedKeys.Contains(Keys.ControlKey))
            {
                return false;
            }
        }

        if (gesture.Alt)
        {
            hasModifier = true;
            if (!_pressedKeys.Contains(Keys.Menu))
            {
                return false;
            }
        }

        if (gesture.Shift)
        {
            hasModifier = true;
            if (!_pressedKeys.Contains(Keys.ShiftKey))
            {
                return false;
            }
        }

        if (gesture.Win)
        {
            hasModifier = true;
            if (!_pressedKeys.Contains(Keys.LWin) && !_pressedKeys.Contains(Keys.RWin))
            {
                return false;
            }
        }

        return hasModifier ? true : _pressedKeys.Contains(gesture.Key);
    }

    private bool ShouldRunStaleKeyCleanup() =>
        Environment.TickCount64 - _lastKeyboardEventTick >= 80;

    private void ActivateMode(MarkupMode mode)
    {
        DebugLogger.Log($"activate mode={mode} committedBefore={_committedShapes.Count}");
        CancelFade();
        _previewShape = null;
        _drawing = false;
        _committedShapes.Clear();
        RenderScene(255);
        _activeMode = mode;
        _inputBlocker.ActivateBlocker();
        if (mode == MarkupMode.Rectangle)
        {
            _rectPrimaryKeyPendingRelease = true;
        }
        else if (mode == MarkupMode.HorizontalLine)
        {
            _linePrimaryKeyPendingRelease = true;
        }
        DebugLogger.Log($"activate done mode={_activeMode} committedAfter={_committedShapes.Count}");
    }

    private void FinishMode()
    {
        DebugLogger.Log($"finish mode={_activeMode} committed={_committedShapes.Count}");
        _activeMode = MarkupMode.None;
        _drawing = false;
        _inputBlocker.DeactivateBlocker();
        _previewShape = null;
        _drawTimer.Stop();
        StartFade();
    }

    private bool HandleMouseHook(MouseHookEventArgs args)
    {
        if (_activeMode != MarkupMode.None && ShouldRunStaleKeyCleanup())
        {
            ClearReleasedGestureKeys(_rectangleGesture);
            ClearReleasedGestureKeys(_lineGesture);
            RefreshGestureState();
        }

        if (args.Message != NativeMethods.WmMouseMove && args.Message != NativeMethods.WmNcMouseMove)
        {
            DebugLogger.Log(
                $"mouse msg={args.Message} point={args.ScreenPoint.X},{args.ScreenPoint.Y} active={_activeMode} drawing={_drawing} committed={_committedShapes.Count} keys={FormatPressedKeys()}");
        }

        if (!_settings.Enabled || _activeMode == MarkupMode.None)
        {
            return false;
        }

        return false;
    }

    private void BeginDrawing(Point point)
    {
        DebugLogger.Log($"begin drawing mode={_activeMode} point={point.X},{point.Y} committed={_committedShapes.Count}");
        _lastMousePoint = point;
        _drawing = true;
        _previewShape = CreateShape(point, point);
        _drawTimer.Start();
        RenderScene(255);
    }

    private void EndDrawing(Point point)
    {
        if (!_drawing)
        {
            DebugLogger.Log($"end drawing ignored point={point.X},{point.Y} because drawing=false");
            return;
        }

        _drawing = false;
        _drawTimer.Stop();

        if (_previewShape is null)
        {
            DebugLogger.Log($"end drawing ignored point={point.X},{point.Y} because preview=null");
            return;
        }

        var completed = CreateShape(new Point(_previewShape.X1, _previewShape.Y1), point);
        _committedShapes.Add(completed);
        _previewShape = null;
        DebugLogger.Log($"end drawing committed point={point.X},{point.Y} committed={_committedShapes.Count}");
        RenderScene(255);
    }

    private void UpdatePreview()
    {
        if (_activeMode == MarkupMode.None || !_drawing || _previewShape is null)
        {
            return;
        }

        var current = _lastMousePoint;
        _previewShape = CreateShape(new Point(_previewShape.X1, _previewShape.Y1), current);
        RenderScene(255);
    }

    private MarkupShape CreateShape(Point start, Point end) =>
        new(
            _activeMode,
            start.X,
            start.Y,
            end.X,
            _activeMode == MarkupMode.HorizontalLine ? start.Y : end.Y,
            _settings.GetLineWidth());

    private void StartFade()
    {
        if (_committedShapes.Count == 0)
        {
            DebugLogger.Log("start fade skipped because committed=0");
            RenderScene(255);
            return;
        }

        _fadeAlpha = 255;
        DebugLogger.Log($"start fade committed={_committedShapes.Count}");
        _fadeTimer.Start();
    }

    private void CancelFade()
    {
        _fadeTimer.Stop();
        _fadeAlpha = 255;
    }

    private void FadeTick()
    {
        _fadeAlpha = (byte)Math.Max(0, _fadeAlpha - 28);
        if (_fadeAlpha == 0)
        {
            _fadeTimer.Stop();
            _committedShapes.Clear();
            _previewShape = null;
            DebugLogger.Log("fade complete cleared committed shapes");
            RenderScene(255);
            return;
        }

        RenderScene(_fadeAlpha);
    }

    private void RenderScene(byte alpha)
    {
        _overlayForm.UpdateScene(_committedShapes, _previewShape, _settings.GetDrawingColor(), alpha);
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsStore.Save(_settings);
        DebugLogger.Log($"toggle enabled={_settings.Enabled}");

        if (!_settings.Enabled)
        {
            ResetDrawingState(clearShapes: true);
        }

        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        _notifyIcon.Icon = _settings.Enabled ? _enabledIcon : _pausedIcon;
        _notifyIcon.Text = _settings.Enabled
            ? $"LiteMark 已启用 - 矩形 {_rectangleGesture} / 横线 {_lineGesture}"
            : "LiteMark 已暂停";
    }

    private void ToggleStartup()
    {
        var executablePath = Application.ExecutablePath;
        var newState = !_startupManager.IsEnabled();
        _startupManager.SetEnabled(newState, executablePath);
        UpdateStartupMenuState();
    }

    private void UpdateStartupMenuState()
    {
        _startupMenuItem.Checked = _startupManager.IsEnabled();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _settings.Enabled ? _enabledIcon : _pausedIcon);
        if (form.ShowDialog() != DialogResult.OK || form.UpdatedSettings is null)
        {
            return;
        }

        var enabled = _settings.Enabled;
        _settings = form.UpdatedSettings;
        _settings.Enabled = enabled;
        _settingsStore.Save(_settings);
        DebugLogger.Log($"settings updated rect={_rectangleGesture} line={_lineGesture}");

        _rectangleGesture = _settings.GetRectangleGesture();
        _lineGesture = _settings.GetLineGesture();

        ResetDrawingState(clearShapes: true);
        UpdateTrayState();
    }

    private void CancelActiveSession()
    {
        ResetDrawingState(clearShapes: true);
    }

    private void ResetDrawingState(bool clearShapes)
    {
        DebugLogger.Log($"reset drawing clearShapes={clearShapes} active={_activeMode} committed={_committedShapes.Count}");
        CancelFade();
        _drawTimer.Stop();
        _inputBlocker.DeactivateBlocker();
        _previewShape = null;
        _drawing = false;
        _activeMode = MarkupMode.None;
        _rectPressed = false;
        _linePressed = false;
        _rectPrimaryKeyPendingRelease = false;
        _linePrimaryKeyPendingRelease = false;
        _pressedKeys.Clear();
        if (clearShapes)
        {
            _committedShapes.Clear();
        }

        RenderScene(255);
    }

    private void HandleBlockedPointerMoved(Point point)
    {
        _lastMousePoint = point;
    }

    private void HandleBlockedPointerPressed(Point point)
    {
        if (!_settings.Enabled || _activeMode == MarkupMode.None)
        {
            return;
        }

        BeginDrawing(point);
    }

    private void HandleBlockedPointerReleased(Point point)
    {
        if (!_settings.Enabled || _activeMode == MarkupMode.None)
        {
            return;
        }

        _lastMousePoint = point;
        EndDrawing(point);
    }

    private string FormatPressedKeys() =>
        _pressedKeys.Count == 0
            ? "-"
            : string.Join(",", _pressedKeys.OrderBy(static key => key.ToString()).Select(static key => key.ToString()));

    private void UpdatePrimaryKeyCaptureState(KeyStateChangedEventArgs e)
    {
        if (e.Key == _rectangleGesture.Key)
        {
            if (e.IsDown && (_activeMode == MarkupMode.Rectangle || _rectangleGesture.IsPressed(_pressedKeys)))
            {
                _rectPrimaryKeyPendingRelease = true;
            }
            else if (!e.IsDown)
            {
                _rectPrimaryKeyPendingRelease = false;
            }
        }

        if (e.Key == _lineGesture.Key)
        {
            if (e.IsDown && (_activeMode == MarkupMode.HorizontalLine || _lineGesture.IsPressed(_pressedKeys)))
            {
                _linePrimaryKeyPendingRelease = true;
            }
            else if (!e.IsDown)
            {
                _linePrimaryKeyPendingRelease = false;
            }
        }
    }
}
