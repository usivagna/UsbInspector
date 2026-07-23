using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace UsbInspector.Core.Interop;

/// <summary>Thin helpers over the Configuration Manager (CfgMgr32) devnode APIs.</summary>
public static class CmDevice
{
    /// <summary>Retrieves the device instance ID (e.g. <c>USB\VID_1234&amp;PID_5678\...</c>) for a devnode.</summary>
    public static unsafe string GetDeviceId(uint devInst)
    {
        if (PInvoke.CM_Get_Device_ID_Size(out uint len, devInst, 0) != CONFIGRET.CR_SUCCESS || len == 0)
        {
            return string.Empty;
        }

        char[] buffer = new char[len + 1];
        CONFIGRET cr = PInvoke.CM_Get_Device_IDW(devInst, buffer, 0);
        if (cr != CONFIGRET.CR_SUCCESS)
        {
            return string.Empty;
        }

        int nul = Array.IndexOf(buffer, '\0');
        return new string(buffer, 0, nul < 0 ? buffer.Length : nul);
    }

    public static uint? GetParent(uint devInst)
        => PInvoke.CM_Get_Parent(out uint parent, devInst, 0) == CONFIGRET.CR_SUCCESS ? parent : null;

    public static uint? GetChild(uint devInst)
        => PInvoke.CM_Get_Child(out uint child, devInst, 0) == CONFIGRET.CR_SUCCESS ? child : null;

    public static uint? GetSibling(uint devInst)
        => PInvoke.CM_Get_Sibling(out uint sibling, devInst, 0) == CONFIGRET.CR_SUCCESS ? sibling : null;

    /// <summary>Enumerates a devnode and all of its descendants (depth-first).</summary>
    public static IEnumerable<uint> EnumerateSubtree(uint devInst)
    {
        yield return devInst;
        uint? child = GetChild(devInst);
        while (child is uint c)
        {
            foreach (uint descendant in EnumerateSubtree(c))
            {
                yield return descendant;
            }

            child = GetSibling(c);
        }
    }

    /// <summary>Resolves a device instance ID string to a devnode handle.</summary>
    public static unsafe uint? LocateDevNode(string instanceId)
    {
        fixed (char* p = instanceId)
        {
            CONFIGRET cr = PInvoke.CM_Locate_DevNode(out uint devInst, new PWSTR(p), CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_NORMAL);
            return cr == CONFIGRET.CR_SUCCESS ? devInst : null;
        }
    }

    public static (CM_DEVNODE_STATUS_FLAGS Status, CM_PROB Problem)? GetStatus(uint devInst)
    {
        CONFIGRET cr = PInvoke.CM_Get_DevNode_Status(out CM_DEVNODE_STATUS_FLAGS status, out CM_PROB problem, devInst, 0);
        return cr == CONFIGRET.CR_SUCCESS ? (status, problem) : null;
    }
}
