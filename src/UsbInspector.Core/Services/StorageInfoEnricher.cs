using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Read-only, best-effort computer and USB-backed physical disk discovery.</summary>
public sealed class StorageInfoEnricher
{
    private const uint IoctlDiskIsWritable = 0x00070024;
    private const int ErrorWriteProtect = 19;
    private static readonly Guid DiskInterfaceClass = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");

    public void Enrich(UsbSnapshot snapshot)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2", "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            using var computers = searcher.Get();
            foreach (ManagementObject computer in computers)
            {
                using (computer)
                {
                    snapshot.ComputerManufacturer = Text(computer["Manufacturer"]);
                    snapshot.ComputerModel = Text(computer["Model"]);
                }
                break;
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add($"Computer model discovery failed: {ex.Message}");
        }

        try
        {
            UsbNode[] nodes = snapshot.EnumerateAll().ToArray();
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT DeviceID, PNPDeviceID, Manufacturer, Model, SerialNumber, Size FROM Win32_DiskDrive");
            using var disks = searcher.Get();
            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    UsbNode? node = null;
                    string? deviceId = null;
                    try
                    {
                        deviceId = Text(disk["DeviceID"]);
                        string? pnpId = Text(disk["PNPDeviceID"]);
                        if (pnpId is null)
                        {
                            snapshot.Warnings.Add($"Storage identity unavailable for {deviceId ?? "unknown disk"}.");
                            continue;
                        }
                        IReadOnlyList<string> ancestry = ReadAncestry(pnpId);
                        node = FindNearestUsbNode(pnpId, ancestry, nodes);
                        if (node is null)
                        {
                            if (ancestry.Any(id => id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)))
                            {
                                snapshot.Warnings.Add(
                                    $"Storage disk {deviceId ?? pnpId} could not be uniquely matched to a USB device.");
                            }
                            continue;
                        }

                        var storage = new UsbStorageInfo
                        {
                            DeviceId = deviceId,
                            Manufacturer = Text(disk["Manufacturer"]),
                            Model = Text(disk["Model"]),
                            SerialNumber = Text(disk["SerialNumber"]),
                            CapacityBytes = Size(disk["Size"]),
                        };
                        node.StorageDevices.Add(storage);
                        ReadWriteProtection(pnpId, storage, node.Warnings);
                        ReadVolumes(disk, storage, node.Warnings);
                    }
                    catch (Exception ex)
                    {
                        (node?.Warnings ?? snapshot.Warnings).Add(
                            $"Storage discovery failed for {deviceId ?? "unknown disk"}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add($"Storage discovery failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Matches exact instance IDs along a disk-first CM ancestry chain, including SCSI/UASP.
    /// Ambiguous matches and infrastructure nodes are barriers, never fallback targets.
    /// </summary>
    public static UsbNode? FindNearestUsbNode(
        string? diskPnpId, IReadOnlyList<string> ancestry, IEnumerable<UsbNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(diskPnpId) || ancestry.Count == 0 ||
            !string.Equals(diskPnpId, ancestry[0], StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The USB4 tree also contains generic PnP aliases of disks and USB devices.
        // Only topology devices and actual infrastructure participate in matching.
        UsbNode[] candidates = nodes.Where(n => n.Kind is UsbNodeKind.Device or UsbNodeKind.RootHub
            or UsbNodeKind.ExternalHub or UsbNodeKind.HostController or UsbNodeKind.Usb4HostRouter)
            .Distinct().ToArray();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ancestry)
        {
            if (string.IsNullOrWhiteSpace(id) || !visited.Add(id))
            {
                return null;
            }

            UsbNode[] matches = candidates.Where(n =>
                !string.IsNullOrWhiteSpace(n.InstanceId) &&
                string.Equals(n.InstanceId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
            {
                continue;
            }

            return matches.Length == 1 && matches[0].Kind == UsbNodeKind.Device && !matches[0].IsHub
                ? matches[0]
                : null;
        }

        return null;
    }

    /// <summary>Only success and ERROR_WRITE_PROTECT determine writability; all other errors are unknown.</summary>
    public static bool? InterpretWriteProtection(bool succeeded, int errorCode) =>
        succeeded ? false : errorCode == ErrorWriteProtect ? true : null;

    /// <summary>Resolves only an unambiguous disk interface belonging to the exact WMI PnP identity.</summary>
    public static string? FindDiskInterfacePath(string? diskPnpId, IEnumerable<DeviceInterfaceInstance> interfaces)
    {
        if (string.IsNullOrWhiteSpace(diskPnpId))
        {
            return null;
        }

        string[] paths = interfaces.Where(i =>
                string.Equals(i.InstanceId, diskPnpId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(i.DevicePath))
            .Select(i => i.DevicePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return paths.Length == 1 ? paths[0] : null;
    }

    private static IReadOnlyList<string> ReadAncestry(string? pnpId)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(pnpId))
        {
            return result;
        }

        uint? current = CmDevice.LocateDevNode(pnpId);
        if (current is null)
        {
            throw new InvalidOperationException($"Cannot locate disk devnode {pnpId}.");
        }

        var visited = new HashSet<uint>();
        while (current is uint devInst && visited.Add(devInst))
        {
            string id = CmDevice.GetDeviceId(devInst);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"Cannot read disk ancestor identity for {pnpId}.");
            }
            result.Add(id);
            current = CmDevice.GetParent(devInst);
        }
        return result;
    }

    private static void ReadWriteProtection(string pnpId, UsbStorageInfo storage, List<string> warnings)
    {
        try
        {
            string? path = FindDiskInterfacePath(pnpId, DeviceInterfaceEnumerator.Enumerate(DiskInterfaceClass));
            if (path is null)
            {
                throw new InvalidOperationException("An unambiguous disk interface is unavailable.");
            }

            // A disk number can be reused after hotplug. Open the exact PnP interface instead,
            // with zero desired access and a query-only IOCTL; never fall back to PhysicalDriveN.
            using var handle = NativeDevice.Open(path, writeAccess: false);
            if (handle.IsInvalid)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
            }

            bool succeeded = NativeDevice.Control(handle, IoctlDiskIsWritable, Array.Empty<byte>(), out _);
            int errorCode = Marshal.GetLastPInvokeError();
            storage.IsReadOnly = InterpretWriteProtection(succeeded, errorCode);
            if (storage.IsReadOnly is null)
            {
                throw new System.ComponentModel.Win32Exception(errorCode);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Read-only status unavailable for {storage.DeviceId}: {ex.Message}");
        }
    }

    private static void ReadVolumes(ManagementObject disk, UsbStorageInfo storage, List<string> warnings)
    {
        try
        {
            using var partitions = disk.GetRelated(
                "Win32_DiskPartition", "Win32_DiskDriveToDiskPartition", null, null, null, null, false, null);
            var seenVolumes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ManagementObject partition in partitions)
            {
                using (partition)
                {
                    try
                    {
                        using var volumes = partition.GetRelated(
                            "Win32_LogicalDisk", "Win32_LogicalDiskToPartition", null, null, null, null, false, null);
                        foreach (ManagementObject volume in volumes)
                        {
                            using (volume)
                            {
                                string? identity = volume.Path?.Path;
                                if (!string.IsNullOrWhiteSpace(identity) && !seenVolumes.Add(identity))
                                {
                                    continue;
                                }
                                storage.Volumes.Add(new UsbVolumeInfo
                                {
                                    DriveLetter = Text(volume["DeviceID"]),
                                    Label = Text(volume["VolumeName"]),
                                    FileSystem = Text(volume["FileSystem"]),
                                    CapacityBytes = Size(volume["Size"]),
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Volume discovery failed for {storage.DeviceId}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Partition discovery failed for {storage.DeviceId}: {ex.Message}");
        }
    }

    private static string? Text(object? value) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? null : value.ToString()!.Trim();

    private static ulong? Size(object? value) =>
        ulong.TryParse(Text(value), NumberStyles.None, CultureInfo.InvariantCulture, out ulong size) ? size : null;
}
