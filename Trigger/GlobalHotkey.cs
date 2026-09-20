using System.Runtime.InteropServices;

namespace FlashGrab.Trigger;

[Flags]
internal enum ModifierKeys : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// 喚醒層:透過 RegisterHotKey 註冊系統級全域快捷鍵。
/// 閒置時不佔 CPU,僅在按下時收到 WM_HOTKEY 訊息。
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x9001;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly HotkeyWindow _window;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkey()
    {
        _window = new HotkeyWindow();
        _window.HotkeyMessage += () => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>最近一次 <see cref="Register"/> 失敗的 Win32 錯誤碼(1409 = 已被其他程式註冊)。</summary>
    public int LastError { get; private set; }

    /// <summary>註冊快捷鍵;已註冊過則先解除再換新的。失敗時舊的不保留(呼叫端負責回復)。</summary>
    public bool Register(HotkeySpec spec)
    {
        Unregister();
        _registered = RegisterHotKey(_window.Handle, HotkeyId, (uint)spec.Modifiers | MOD_NOREPEAT, (uint)spec.Key);
        LastError = _registered ? 0 : Marshal.GetLastWin32Error();
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _window.DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>隱藏訊息視窗,專門接收 WM_HOTKEY。</summary>
    private sealed class HotkeyWindow : NativeWindow
    {
        public event Action? HotkeyMessage;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HotkeyId)
            {
                HotkeyMessage?.Invoke();
            }

            base.WndProc(ref m);
        }
    }
}
