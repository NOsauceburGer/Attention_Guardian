using System;

namespace AttentionGuardian.Desktop.Views;

internal static class MotionPreferences
{
    // Avalonia has no cross-platform reduced-motion abstraction. Keep motion enabled
    // until a macOS accessibility adapter is introduced rather than P/Invoking user32.
    public static bool IsReducedMotionEnabled => false;
}
