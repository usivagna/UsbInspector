using System.Reflection;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;

namespace UsbInspector.Core.Services;

/// <summary>Reads PnP / driver properties for a devnode via CfgMgr32 DEVPKEYs.</summary>
public static class PnpPropertyReader
{
    // Maps (fmtid, pid) -> friendly DEVPKEY name, discovered by reflecting over the
    // generated PInvoke.DEVPKEY_* fields so the raw view can name known keys.
    private static readonly Dictionary<(Guid, uint), string> KnownKeyNames = BuildKnownKeyNames();

    public static PnpDeviceProperties Read(uint devInst)
    {
        var props = new PnpDeviceProperties
        {
            DeviceDescription = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_DeviceDesc),
            FriendlyName = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_FriendlyName),
            BusReportedDeviceDesc = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_BusReportedDeviceDesc),
            Manufacturer = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Manufacturer),
            Service = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Service),
            Class = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Class),
            ClassGuid = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_ClassGuid),
            EnumeratorName = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_EnumeratorName),
            Driver = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Driver),
            DriverVersion = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_DriverVersion),
            DriverDate = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_DriverDate),
            DriverProvider = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_DriverProvider),
            InstanceId = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_InstanceId) ?? CmDevice.GetDeviceId(devInst),
            ContainerId = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_ContainerId),
            LocationInfo = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_LocationInfo),
            PdoName = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_PDOName),
            Address = DevProp.GetUInt32(devInst, PInvoke.DEVPKEY_Device_Address),
            BusNumber = DevProp.GetUInt32(devInst, PInvoke.DEVPKEY_Device_BusNumber),
            HardwareIds = DevProp.GetStringList(devInst, PInvoke.DEVPKEY_Device_HardwareIds),
            CompatibleIds = DevProp.GetStringList(devInst, PInvoke.DEVPKEY_Device_CompatibleIds),
            LocationPaths = DevProp.GetStringList(devInst, PInvoke.DEVPKEY_Device_LocationPaths),
            IsPresent = DevProp.GetBoolean(devInst, PInvoke.DEVPKEY_Device_IsPresent),
            ProblemCode = DevProp.GetUInt32(devInst, PInvoke.DEVPKEY_Device_ProblemCode),
        };

        (CM_DEVNODE_STATUS_FLAGS Status, CM_PROB Problem)? status = CmDevice.GetStatus(devInst);
        if (status is { } s)
        {
            props.StatusFlags = s.Status.ToString();
            if (s.Problem != 0)
            {
                props.ProblemCode ??= (uint)s.Problem;
                props.ProblemDescription = s.Problem.ToString();
            }
        }

        ReadAllKeys(devInst, props);
        return props;
    }

    private static unsafe void ReadAllKeys(uint devInst, PnpDeviceProperties props)
    {
        uint count = 0;
        PInvoke.CM_Get_DevNode_Property_Keys(devInst, null, ref count, 0);
        if (count == 0)
        {
            return;
        }

        var keys = new DEVPROPKEY[count];
        fixed (DEVPROPKEY* p = keys)
        {
            CONFIGRET cr = PInvoke.CM_Get_DevNode_Property_Keys(devInst, p, ref count, 0);
            if (cr != CONFIGRET.CR_SUCCESS)
            {
                return;
            }
        }

        for (uint i = 0; i < count; i++)
        {
            DEVPROPKEY key = keys[i];
            if (!DevProp.TryGet(devInst, in key, out DEVPROPTYPE type, out byte[] data))
            {
                continue;
            }

            string name = KnownKeyNames.TryGetValue((key.fmtid, key.pid), out string? friendly)
                ? friendly
                : $"{key.fmtid:B}#{key.pid}";

            props.AllProperties[name] = DevProp.Format(type, data);
        }
    }

    private static Dictionary<(Guid, uint), string> BuildKnownKeyNames()
    {
        var map = new Dictionary<(Guid, uint), string>();
        foreach (FieldInfo field in typeof(PInvoke).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(DEVPROPKEY) && field.Name.StartsWith("DEVPKEY", StringComparison.Ordinal))
            {
                var key = (DEVPROPKEY)field.GetValue(null)!;
                map[(key.fmtid, key.pid)] = field.Name;
            }
        }

        return map;
    }
}
