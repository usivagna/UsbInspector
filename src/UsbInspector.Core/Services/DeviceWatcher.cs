using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;

namespace UsbInspector.Core.Services;

/// <summary>A live USB device arrival or removal event.</summary>
public sealed class UsbDeviceEvent
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool Arrived { get; init; }
    public string SymbolicLink { get; init; } = string.Empty;

    public string Description => $"{TimestampUtc.ToLocalTime():HH:mm:ss}  {(Arrived ? "Arrived" : "Removed")}  {SymbolicLink}";
}

/// <summary>
/// Watches for live USB device interface arrivals/removals using CM_Register_Notification.
/// No window or message pump is required; the OS invokes the callback on a worker thread.
/// </summary>
public sealed unsafe class DeviceWatcher : IDisposable
{
    private const uint CM_NOTIFY_FILTER_FLAG_ALL_INTERFACE_CLASSES = 0x00000001;

    private CM_Unregister_NotificationSafeHandle? _handle;
    private GCHandle _self;

    /// <summary>Raised on a worker thread when a USB device is plugged in or removed.</summary>
    public event Action<UsbDeviceEvent>? DeviceChanged;

    public bool Start()
    {
        if (_handle is not null)
        {
            return true;
        }

        _self = GCHandle.Alloc(this, GCHandleType.Normal);

        var filter = new CM_NOTIFY_FILTER
        {
            cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
            Flags = CM_NOTIFY_FILTER_FLAG_ALL_INTERFACE_CLASSES,
            FilterType = CM_NOTIFY_FILTER_TYPE.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
        };

        CONFIGRET cr = PInvoke.CM_Register_Notification(
            in filter,
            (void*)GCHandle.ToIntPtr(_self),
            &Callback,
            out CM_Unregister_NotificationSafeHandle handle);

        if (cr != CONFIGRET.CR_SUCCESS)
        {
            _self.Free();
            return false;
        }

        _handle = handle;
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint Callback(
        HCMNOTIFICATION notify,
        void* context,
        CM_NOTIFY_ACTION action,
        CM_NOTIFY_EVENT_DATA* eventData,
        uint eventDataSize)
    {
        try
        {
            if (context is null || eventData is null)
            {
                return 0;
            }

            bool arrived = action == CM_NOTIFY_ACTION.CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL;
            bool removed = action == CM_NOTIFY_ACTION.CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL;
            if (!arrived && !removed)
            {
                return 0;
            }

            if (eventData->FilterType != CM_NOTIFY_FILTER_TYPE.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE)
            {
                return 0;
            }

            char* symbolicLinkPtr = (char*)Unsafe.AsPointer(ref eventData->u.DeviceInterface.SymbolicLink);
            string symbolicLink = new string(symbolicLinkPtr);

            // Restrict to USB interfaces (ALL_INTERFACE_CLASSES delivers every class).
            if (!symbolicLink.StartsWith(@"\\?\USB", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var watcher = (DeviceWatcher?)GCHandle.FromIntPtr((IntPtr)context).Target;
            watcher?.DeviceChanged?.Invoke(new UsbDeviceEvent { Arrived = arrived, SymbolicLink = symbolicLink });
        }
        catch
        {
            // Never let exceptions cross the native boundary.
        }

        return 0;
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }
}
