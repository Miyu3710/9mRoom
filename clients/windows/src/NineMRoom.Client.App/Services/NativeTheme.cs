using System.Runtime.InteropServices;

namespace NineMRoom.Client.App;

internal static class NativeTheme
{
    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    public static void EnableDarkMenus()
    {
        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
            {
                SetPreferredAppMode(2); // ForceDark
                FlushMenuThemes();
            }
        }
        catch
        {
        }
    }
}
