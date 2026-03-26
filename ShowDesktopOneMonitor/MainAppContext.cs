using FrigoTab;
using ShowDesktopOneMonitor.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    public class MainAppContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly MenuItem administratorModeMenuItem;
        private readonly MenuItem launchAtStartupMenuItem;
        private readonly MenuItem useWinDMenuItem;
        private List<DesktopWindowID>[] PrevStateByScreen = new List<DesktopWindowID>[0];
        private int hotKeyRegistrationId;

        public MainAppContext ()
        {
            Application.ThreadException += this.Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;

            administratorModeMenuItem = new MenuItem("\u7ba1\u7406\u5458\u6743\u9650", OnAdministratorModeMenuClick);
            launchAtStartupMenuItem = new MenuItem("\u5f00\u673a\u542f\u52a8", OnLaunchAtStartupMenuClick);
            useWinDMenuItem = new MenuItem("\u4f7f\u7528 Win+D \u66ff\u4ee3\u7cfb\u7edf\u663e\u793a\u684c\u9762", OnUseWinDMenuClick);

            trayIcon = new NotifyIcon() {
                Icon = Resources.sde,
                ContextMenu = new ContextMenu(new MenuItem[] {
                    administratorModeMenuItem,
                    launchAtStartupMenuItem,
                    useWinDMenuItem,
                    new MenuItem("-"),
                    new MenuItem("Exit", (s, e) => ExitApplication()),
                }),
                Visible = true,
                Text = "Show Desktop Enhanced",
            };

            PrevStateByScreen = new List<DesktopWindowID>[Screen.AllScreens.Length];
            administratorModeMenuItem.Checked = Program.IsRunningAsAdministrator();
            launchAtStartupMenuItem.Checked = SettingsManager.ReadLaunchAtStartup();
            useWinDMenuItem.Checked = SettingsManager.ReadUseWinD();

            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(OnHotkeyPressed);
            RegisterConfiguredHotKey();
        }

        private void OnHotkeyPressed (object sender, HotKeyEventArgs e)
        {
            OnShowDesktopKeyComb();
        }

        private void OnShowDesktopKeyComb ()
        {
            Console.WriteLine("Toogling hide windows on screen...");

            Screen currentScreen = Screen.FromPoint(Cursor.Position);
            int screenIdx = Array.IndexOf(Screen.AllScreens, currentScreen);
            List<WindowHandle> windows = GetWindowsOnScreen(currentScreen);
            List<DesktopWindowID> newWindowIDs = ConvertWindowsToIDs(windows);

            Array.Resize(ref PrevStateByScreen, Screen.AllScreens.Length);

            if (newWindowIDs.All(x => x.WindowStyle != WindowStyles.Visible)
                && PrevStateByScreen[screenIdx] != null
                && DoesPrevStateDiffersOnlyByWindowsStyle(newWindowIDs, screenIdx)) {
                RestoreAllWindows(screenIdx);
            }
            else {
                MinimizeAllWindows(newWindowIDs, screenIdx);
            }
        }

        private void MinimizeAllWindows (List<DesktopWindowID> windowList, int screenIdx)
        {
            windowList = windowList
                .Select(x => new { window = x, zOrder = WindowApi.GetWindowZOrder(x.WindowHandle) })
                .OrderByDescending(x => x.zOrder)
                .Select(x => x.window)
                .ToList();

            foreach (var window in windowList) {
                if (window.WindowStyle == WindowStyles.Visible) {
                    window.SourceHandleObj.SetMinimizeWindow();
                }
            }

            PrevStateByScreen[screenIdx] = windowList;
        }

        private void RestoreAllWindows (int screenIdx)
        {
            if (PrevStateByScreen[screenIdx] != null) {
                foreach (var window in PrevStateByScreen[screenIdx].Reverse<DesktopWindowID>()) {
                    if (window.WindowStyle == WindowStyles.Visible) {
                        window.SourceHandleObj.SetRestoreWindow();
                    }
                }
            }

            PrevStateByScreen[screenIdx] = null;
        }

        private void RegisterConfiguredHotKey ()
        {
            if (hotKeyRegistrationId != 0) {
                HotKeyManager.UnregisterHotKey(hotKeyRegistrationId);
            }

            hotKeyRegistrationId = HotKeyManager.RegisterHotKey(SettingsManager.ReadHotkey(), SettingsManager.ReadKeyModifiers());
        }

        private void OnAdministratorModeMenuClick (object sender, EventArgs e)
        {
            if (Program.IsRunningAsAdministrator()) {
                administratorModeMenuItem.Checked = true;
                MessageBox.Show("\u5f53\u524d\u7a0b\u5e8f\u5df2\u7ecf\u4ee5\u7ba1\u7406\u5458\u6743\u9650\u8fd0\u884c\u3002", "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool restarted = Program.RestartAsAdministrator();
            if (restarted) {
                ExitApplication();
                return;
            }

            MessageBox.Show("\u5df2\u53d6\u6d88\u7ba1\u7406\u5458\u6388\u6743\u3002", "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnLaunchAtStartupMenuClick (object sender, EventArgs e)
        {
            bool enabled = !launchAtStartupMenuItem.Checked;

            try {
                SettingsManager.WriteLaunchAtStartup(enabled);
                SettingsManager.Save();
                launchAtStartupMenuItem.Checked = enabled;
            }
            catch (Exception ex) {
                MessageBox.Show("\u8bbe\u7f6e\u5f00\u673a\u542f\u52a8\u5931\u8d25\uff1a" + ex.Message, "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUseWinDMenuClick (object sender, EventArgs e)
        {
            bool enabled = !useWinDMenuItem.Checked;

            SettingsManager.WriteUseWinD(enabled);
            SettingsManager.Save();
            useWinDMenuItem.Checked = enabled;
            RegisterConfiguredHotKey();

            string message = enabled
                ? "\u5df2\u542f\u7528 Win+D\u3002\u73b0\u5728\u6309 Win+D \u5c06\u7531\u672c\u7a0b\u5e8f\u63a5\u7ba1\uff0c\u7528\u4e8e\u5f53\u524d\u5c4f\u5e55\u7684\u663e\u793a\u684c\u9762\u3002"
                : "\u5df2\u5173\u95ed Win+D \u63a5\u7ba1\u3002\u73b0\u5728\u6062\u590d\u4f7f\u7528 Win+Shift+D\uff0c\u7cfb\u7edf\u539f\u751f Win+D \u4e5f\u4f1a\u6062\u590d\u3002";

            MessageBox.Show(message, "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApplication ()
        {
            if (hotKeyRegistrationId != 0) {
                HotKeyManager.UnregisterHotKey(hotKeyRegistrationId);
                hotKeyRegistrationId = 0;
            }

            trayIcon.Visible = false;
            SettingsManager.Save();
            Application.Exit();
        }

        private List<WindowHandle> GetWindowsOnScreen (Screen screen)
        {
            WindowFinder finder = new WindowFinder();
            return finder.Windows.Where(x => x.GetScreen().Equals(screen)).ToList();
        }

        private List<DesktopWindowID> ConvertWindowsToIDs (List<WindowHandle> windows)
        {
            List<DesktopWindowID> list = new List<DesktopWindowID>(windows.Count);
            for (int i = 0; i < windows.Count; i++) {
                var window = windows[i];
                list.Add(new DesktopWindowID(window));
            }

            return list;
        }

        private bool DoesPrevStateDiffersOnlyByWindowsStyle (List<DesktopWindowID> newList, int screenIdx)
        {
            if (PrevStateByScreen[screenIdx] == null) return false;
            if (newList.Count != PrevStateByScreen[screenIdx].Count) return false;
            if (false == newList.All(x => PrevStateByScreen[screenIdx].Contains(x))) return false;

            return false == newList.All(x => PrevStateByScreen[screenIdx].First(y => y == x).WindowStyle.Equals(x.WindowStyle));
        }

        ~MainAppContext ()
        {
            SettingsManager.Save();
        }

        private void Application_ThreadException (object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("Unhandled exception: " + e.Exception, "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Unhandled exception: " + (e.ExceptionObject as Exception), "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public class DesktopWindowID : IEquatable<DesktopWindowID>
    {
        public WindowHandle SourceHandleObj;
        public IntPtr WindowHandle = IntPtr.Zero;
        public WindowStyles WindowStyle = WindowStyles.Disabled;

        public DesktopWindowID (WindowHandle sourceHandleObj)
        {
            this.SourceHandleObj = sourceHandleObj;
            this.WindowHandle = this.SourceHandleObj.GetHandle();
            this.WindowStyle = WindowStyles.Disabled;

            var wndStyle = this.SourceHandleObj.GetWindowStyles();
            if (wndStyle.HasFlag(WindowStyles.Minimize))
                this.WindowStyle = WindowStyles.Minimize;
            else if (wndStyle.HasFlag(WindowStyles.Visible))
                this.WindowStyle = WindowStyles.Visible;
        }

        public bool Equals (DesktopWindowID other)
        {
            return Equals((object)other);
        }

        public override int GetHashCode ()
        {
            return this.WindowHandle.GetHashCode();
        }

        public override bool Equals (object obj)
        {
            if (obj == null) return false;
            return this.GetHashCode() == obj.GetHashCode();
        }

        public static bool operator ==(DesktopWindowID obj1, DesktopWindowID obj2)
        {
            if (ReferenceEquals(obj1, null)) {
                return ReferenceEquals(obj2, null);
            }

            return obj1.Equals(obj2);
        }

        public static bool operator !=(DesktopWindowID obj1, DesktopWindowID obj2)
        {
            return (obj1 == obj2) == false;
        }
    }
}
