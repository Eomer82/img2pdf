using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Img2Pdf.App.Services;

public static class ShellIntegrationService
{
    private const string ImageKeyPath = @"Software\Classes\SystemFileAssociations\image\shell\Img2Pdf";
    private const string DirectoryKeyPath = @"Software\Classes\Directory\shell\Img2Pdf";

    public static bool IsContextMenuRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ImageKeyPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetContextMenuRegistered(bool enable)
    {
        if (enable)
        {
            RegisterContextMenu();
        }
        else
        {
            UnregisterContextMenu();
        }
    }

    public static void RegisterContextMenu()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                          ?? Environment.ProcessPath 
                          ?? string.Empty;

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return;
            }

            string menuTitle = LocalizationService.GetString("ContextMenuTitle");

            // 1. For Image files
            using (var key = Registry.CurrentUser.CreateSubKey(ImageKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(string.Empty, menuTitle);
                    key.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = key.CreateSubKey("command");
                    cmdKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
                }
            }

            // 2. For Directories
            using (var dirKey = Registry.CurrentUser.CreateSubKey(DirectoryKeyPath))
            {
                if (dirKey != null)
                {
                    dirKey.SetValue(string.Empty, menuTitle);
                    dirKey.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = dirKey.CreateSubKey("command");
                    cmdKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
                }
            }
        }
        catch
        {
            // Silently ignore if registry write fails
        }
    }

    public static void UnregisterContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ImageKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryKeyPath, false);
        }
        catch { }
    }
}
