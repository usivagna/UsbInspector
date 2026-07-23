using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace UsbInspector.Core.Interop;

/// <summary>Determines whether the current process is running elevated (as Administrator).</summary>
public static class Elevation
{
    public static unsafe bool IsProcessElevated()
    {
        HANDLE process = PInvoke.GetCurrentProcess();
        HANDLE token;
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
        {
            return false;
        }

        try
        {
            TOKEN_ELEVATION elevation = default;
            uint returnLength = 0;
            BOOL ok = PInvoke.GetTokenInformation(
                token,
                TOKEN_INFORMATION_CLASS.TokenElevation,
                &elevation,
                (uint)sizeof(TOKEN_ELEVATION),
                &returnLength);

            return ok && elevation.TokenIsElevated != 0;
        }
        finally
        {
            PInvoke.CloseHandle(token);
        }
    }
}
