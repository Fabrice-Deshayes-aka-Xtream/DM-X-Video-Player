using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Services
{
    /// <summary>
    /// Manages the association of DM-X Video Player with video file extensions on Windows.
    /// All registry entries are written under HKEY_CURRENT_USER so no administrator
    /// elevation is required. The application is registered through the standard
    /// "RegisteredApplications" mechanism, and each supported extension is declared so
    /// Windows actually lists DM-X Video Player as a candidate in Settings > Apps > Default apps
    /// (Windows 10/11 no longer allows an application to silently force itself as default,
    /// but it still requires the extension to be declared to offer it as a choice).
    /// </summary>
    public static class FileAssociationService
    {
        private const string ProgId = "DMXVideoPlayer.Video";
        private const string ApplicationRegistryKey = "DMXVideoPlayer";
        private const string ApplicationDisplayName = "DM-X Video Player";

        /// <summary>
        /// Common video file extensions supported by VLC/LibVLC.
        /// </summary>
        public static readonly string[] SupportedVideoExtensions =
        {
            ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
            ".mpg", ".mpeg", ".m2ts", ".mts", ".ts", ".3gp", ".vob"
        };

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static string ExecutablePath => Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Unable to determine the application executable path.");

        /// <summary>
        /// Registers DM-X Video Player as a candidate handler for the common video
        /// extensions supported by VLC, and exposes it in Windows' "Default apps" settings.
        /// </summary>
        public static void RegisterFileAssociations()
        {
            string exePath = ExecutablePath;

            // 1. ProgId describing how to open a video file with this application.
            using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                progIdKey.SetValue(string.Empty, $"{ApplicationDisplayName} Video");

                using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(string.Empty, $"\"{exePath}\",0");

                using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
                commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
            }

            // 2. Application "capabilities" (name, description, file associations).
            using (var capabilitiesKey = Registry.CurrentUser.CreateSubKey($@"Software\{ApplicationRegistryKey}\Capabilities"))
            {
                capabilitiesKey.SetValue("ApplicationName", ApplicationDisplayName);
                capabilitiesKey.SetValue("ApplicationDescription", "Classic minimalist video player based on VLC.");

                using var fileAssociationsKey = capabilitiesKey.CreateSubKey("FileAssociations");
                foreach (var extension in SupportedVideoExtensions)
                {
                    fileAssociationsKey.SetValue(extension, ProgId);
                }
            }

            // 3. Register the application so Windows lists it in Settings > Apps > Default apps.
            using (var registeredAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                registeredAppsKey.SetValue(ApplicationRegistryKey, $@"Software\{ApplicationRegistryKey}\Capabilities");
            }

            // 4. Declare the ProgId as an available "Open with" handler for each extension so
            //    Windows actually offers DM-X Video Player in the Default Apps picker.
            foreach (var extension in SupportedVideoExtensions)
            {
                using var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
                openWithKey.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            NotifyShellOfAssociationChanges();
        }

        /// <summary>
        /// Opens the Windows "Default apps" settings page, scrolled directly to
        /// DM-X Video Player's entry so the user can finish selecting it as the
        /// default handler for the registered extensions.
        /// </summary>
        public static void OpenWindowsDefaultAppsSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppUser={ApplicationRegistryKey}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to open the application-specific default apps settings page: {ex.Message}");

                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"Unable to open Windows default apps settings: {fallbackEx.Message}");
                }
            }
        }

        private static void NotifyShellOfAssociationChanges()
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
