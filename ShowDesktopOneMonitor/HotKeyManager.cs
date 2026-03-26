using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    public static class HotKeyManager
    {
        public static event EventHandler<HotKeyEventArgs> HotKeyPressed;

        public static int RegisterHotKey(Keys key, KeyModifiers modifiers)
        {
            _windowReadyEvent.WaitOne();
            int id = Interlocked.Increment(ref _id);
            _wnd.Invoke(new ConfigureHotKeyDelegate(ConfigureHotKeyInternal), key, modifiers);
            return id;
        }

        public static void UnregisterHotKey(int id)
        {
            _windowReadyEvent.WaitOne();
            _wnd.Invoke(new ClearHotKeyDelegate(ClearHotKeyInternal));
        }

        private delegate void ConfigureHotKeyDelegate(Keys key, KeyModifiers modifiers);
        private delegate void ClearHotKeyDelegate();
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static volatile MessageWindow _wnd;
        private static volatile IntPtr _hwnd;
        private static readonly ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);
        private static int _id;
        private static Keys _registeredKey = Keys.None;
        private static KeyModifiers _registeredModifiers;
        private static bool _leftWindowsDown;
        private static bool _rightWindowsDown;
        private static bool _shiftDown;
        private static bool _controlDown;
        private static bool _altDown;
        private static bool _comboTriggered;
        private static bool _suppressWindowsKeys;
        private static IntPtr _hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc _hookProc;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        static HotKeyManager()
        {
            Thread messageLoop = new Thread(delegate ()
            {
                Application.Run(new MessageWindow());
            });
            messageLoop.Name = "MessageLoopThread";
            messageLoop.IsBackground = true;
            messageLoop.Start();
        }

        private static void ConfigureHotKeyInternal(Keys key, KeyModifiers modifiers)
        {
            _registeredKey = key;
            _registeredModifiers = modifiers;
            _comboTriggered = false;
            _suppressWindowsKeys = false;
        }

        private static void ClearHotKeyInternal()
        {
            _registeredKey = Keys.None;
            _registeredModifiers = 0;
            _comboTriggered = false;
            _suppressWindowsKeys = false;
        }

        private static void OnHotKeyPressed(HotKeyEventArgs e)
        {
            HotKeyManager.HotKeyPressed?.Invoke(null, e);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule) {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) {
                int message = wParam.ToInt32();
                KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                Keys key = (Keys)hookStruct.vkCode;
                bool isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                bool isKeyUp = message == WM_KEYUP || message == WM_SYSKEYUP;

                UpdateModifierState(key, isKeyDown, isKeyUp);

                if (ShouldSuppressWindowsKey(key, isKeyUp)) {
                    return new IntPtr(1);
                }

                if (_registeredKey != Keys.None && key == _registeredKey && IsRegisteredCombinationPressed()) {
                    if (isKeyDown && !_comboTriggered) {
                        _comboTriggered = true;
                        _suppressWindowsKeys = true;
                        OnHotKeyPressed(new HotKeyEventArgs(_registeredKey, _registeredModifiers));
                    }

                    if (isKeyUp) {
                        _comboTriggered = false;
                    }

                    return new IntPtr(1);
                }

                if (isKeyUp && !_leftWindowsDown && !_rightWindowsDown) {
                    _comboTriggered = false;
                    _suppressWindowsKeys = false;
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static void UpdateModifierState(Keys key, bool isKeyDown, bool isKeyUp)
        {
            switch (key) {
                case Keys.LWin:
                    if (isKeyDown) _leftWindowsDown = true;
                    if (isKeyUp) _leftWindowsDown = false;
                    break;
                case Keys.RWin:
                    if (isKeyDown) _rightWindowsDown = true;
                    if (isKeyUp) _rightWindowsDown = false;
                    break;
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ShiftKey:
                    if (isKeyDown) _shiftDown = true;
                    if (isKeyUp) _shiftDown = false;
                    break;
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.ControlKey:
                    if (isKeyDown) _controlDown = true;
                    if (isKeyUp) _controlDown = false;
                    break;
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.Menu:
                    if (isKeyDown) _altDown = true;
                    if (isKeyUp) _altDown = false;
                    break;
            }
        }

        private static bool ShouldSuppressWindowsKey(Keys key, bool isKeyUp)
        {
            if (!_suppressWindowsKeys) {
                return false;
            }

            if (key != Keys.LWin && key != Keys.RWin) {
                return false;
            }

            if (isKeyUp && !_leftWindowsDown && !_rightWindowsDown) {
                _suppressWindowsKeys = false;
                _comboTriggered = false;
            }

            return true;
        }

        private static bool IsRegisteredCombinationPressed()
        {
            bool windowsPressed = _leftWindowsDown || _rightWindowsDown;
            bool shiftRequired = (_registeredModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
            bool controlRequired = (_registeredModifiers & KeyModifiers.Control) == KeyModifiers.Control;
            bool altRequired = (_registeredModifiers & KeyModifiers.Alt) == KeyModifiers.Alt;

            return windowsPressed
                && _shiftDown == shiftRequired
                && _controlDown == controlRequired
                && _altDown == altRequired;
        }

        private class MessageWindow : Form
        {
            public MessageWindow()
            {
                _wnd = this;
                _hwnd = this.Handle;
                _hookProc = HookCallback;
                _hookId = SetHook(_hookProc);
                _windowReadyEvent.Set();
            }

            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _hookId != IntPtr.Zero) {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }

                base.Dispose(disposing);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    public class HotKeyEventArgs : EventArgs
    {
        public readonly Keys Key;
        public readonly KeyModifiers Modifiers;

        public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
        {
            this.Key = key;
            this.Modifiers = modifiers;
        }

        public HotKeyEventArgs(IntPtr hotKeyParam)
        {
            uint param = (uint)hotKeyParam.ToInt64();
            Key = (Keys)((param & 0xffff0000) >> 16);
            Modifiers = (KeyModifiers)(param & 0x0000ffff);
        }
    }

    [Flags]
    public enum KeyModifiers
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }
}
