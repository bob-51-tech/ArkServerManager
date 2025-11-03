using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ArkServerManager
{
    public class IniManager
    {
        private readonly string _filePath;
        private const string SectionName = "Settings";

        // Import the necessary Windows API functions for INI file handling
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string Default, StringBuilder retVal, int size, string filePath);

        public IniManager()
        {
            // Create the path to 'settings.ini' in the same directory as the executable
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        }

        public AppSettings LoadSettings()
        {
            var settings = new AppSettings();
            if (!File.Exists(_filePath))
            {
                // If the file doesn't exist, save the default settings and return them
                SaveSettings(settings);
                return settings;
            }

            settings.AutoUpdate = bool.Parse(ReadValue("AutoUpdate", settings.AutoUpdate.ToString()));
            settings.StartWithWindows = bool.Parse(ReadValue("StartWithWindows", settings.StartWithWindows.ToString()));
            settings.MinimizeToTray = bool.Parse(ReadValue("MinimizeToTray", settings.MinimizeToTray.ToString()));
            settings.StartMinimized = bool.Parse(ReadValue("StartMinimized", settings.StartMinimized.ToString()));

            return settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            WriteValue("AutoUpdate", settings.AutoUpdate.ToString());
            WriteValue("StartWithWindows", settings.StartWithWindows.ToString());
            WriteValue("MinimizeToTray", settings.MinimizeToTray.ToString());
            WriteValue("StartMinimized", settings.StartMinimized.ToString());

            // Apply the "Start With Windows" setting immediately
            SetStartup(settings.StartWithWindows);
        }

        private string ReadValue(string key, string defaultValue)
        {
            var retVal = new StringBuilder(255);
            GetPrivateProfileString(SectionName, key, defaultValue, retVal, 255, _filePath);
            return retVal.ToString();
        }

        private void WriteValue(string key, string value)
        {
            WritePrivateProfileString(SectionName, key, value, _filePath);
        }

        /// <summary>
        /// Modifies the Windows Registry to add or remove the application from startup.
        /// </summary>
        public void SetStartup(bool enable)
        {
            try
            {
                // The registry key for the current user's startup programs
                const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                string appName = AppDomain.CurrentDomain.FriendlyName; // e.g., "ArkServerManager.exe"

                RegistryKey startupKey = Registry.CurrentUser.OpenSubKey(runKey, true);
                if (startupKey == null) return;

                if (enable)
                {
                    // Add the value if it doesn't exist
                    if (startupKey.GetValue(appName) == null)
                    {
                        // Get the full path to the executable
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        startupKey.SetValue(appName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    // Remove the value if it exists
                    if (startupKey.GetValue(appName) != null)
                    {
                        startupKey.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception)
            {
                // Could fail due to permissions, etc.
                // You might want to log this error or notify the user.
            }
        }
    }
}