using Microsoft.Win32;

namespace UptimeKumaTrayAgent.Services;

public sealed class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UptimeKumaTrayAgent";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public void Enable(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        var command = $"\"{executablePath}\" --minimized";
        key?.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(ValueName, false);
        }
        catch
        {
            // Registry cleanup is best effort; callers log or display the high-level result.
        }
    }
}
