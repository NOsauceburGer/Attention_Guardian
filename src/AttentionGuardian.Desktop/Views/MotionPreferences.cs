using System;
using System.Runtime.InteropServices;

namespace AttentionGuardian.Desktop.Views;

internal static class MotionPreferences
{
    public static bool IsReducedMotionEnabled { get; } = GetReducedMotion();

    private static bool GetReducedMotion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return !SystemParametersInfo(
                0x1042,
                0,
                out var animationsEnabled,
                0)
                || !animationsEnabled;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool value,
        uint update);
}
