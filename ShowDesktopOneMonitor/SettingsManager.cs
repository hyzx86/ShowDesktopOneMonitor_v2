using Microsoft.Win32;
using ShowDesktopOneMonitor.Properties;
using System;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    /// <summary>
    /// Util class for reading and writing to settings file
    /// </summary>
    static class SettingsManager
    {
        private static readonly Settings SETTINGS = Settings.Default;
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "ShowDesktopOneMonitor";

        public static Keys ReadHotkey ()
        {
            return Keys.D;
        }

        public static KeyModifiers ReadKeyModifiers ()
        {
            return SETTINGS.UseWinD ? KeyModifiers.Windows : KeyModifiers.Windows | KeyModifiers.Shift;
        }

        public static bool ReadUseWinD ()
        {
            return SETTINGS.UseWinD;
        }

        public static void WriteUseWinD (bool useWinD)
        {
            SETTINGS.UseWinD = useWinD;
        }

        public static bool ReadLaunchAtStartup ()
        {
            bool enabled = IsStartupEnabledInRegistry();
            SETTINGS.LaunchAtStartup = enabled;
            return enabled;
        }

        public static void WriteLaunchAtStartup (bool enabled)
        {
            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true)) {
                if (runKey == null) {
                    throw new InvalidOperationException("\u65e0\u6cd5\u6253\u5f00\u542f\u52a8\u9879\u6ce8\u518c\u8868\u3002");
                }

                if (enabled) {
                    runKey.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                }
                else {
                    runKey.DeleteValue(RunValueName, false);
                }
            }

            SETTINGS.LaunchAtStartup = enabled;
        }

        public static void Save () => SETTINGS.Save();

        private static bool IsStartupEnabledInRegistry ()
        {
            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false)) {
                string value = runKey?.GetValue(RunValueName) as string;
                if (string.IsNullOrWhiteSpace(value)) {
                    return false;
                }

                string normalizedValue = value.Trim().Trim('"');
                return string.Equals(normalizedValue, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
