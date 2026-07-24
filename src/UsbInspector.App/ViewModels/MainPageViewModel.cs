using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace UsbInspector_App.ViewModels;

/// <summary>Backs the main page: topology tree, detail panes, live event log and commands.</summary>
public sealed partial class MainPageViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private readonly DeviceWatcher _watcher = new();
    private UsbSnapshot? _snapshot;
    private CancellationTokenSource? _refreshDebounce;

    public MainPageViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _watcher.DeviceChanged += OnDeviceChanged;
        _watcher.Start();
    }

    public ObservableCollection<UsbTreeItem> RootItems { get; } = new();
    public ObservableCollection<DetailRow> OverviewRows { get; } = new();
    public ObservableCollection<DetailRow> PnpRows { get; } = new();
    public ObservableCollection<DetailRow> PowerRows { get; } = new();
    public ObservableCollection<DetailRow> UsbCRows { get; } = new();
    public ObservableCollection<DetailRow> Usb4Rows { get; } = new();
    public ObservableCollection<DetailRow> AlsoHereRows { get; } = new();
    public ObservableCollection<string> EventLog { get; } = new();

    [ObservableProperty] private UsbTreeItem? _selectedItem;
    [ObservableProperty] private string _descriptorsText = string.Empty;
    [ObservableProperty] private string _rawText = string.Empty;
    [ObservableProperty] private string _selectedTitle = "Select a device";
    [ObservableProperty] private string _statusSummary = "Scanning…";
    [ObservableProperty] private string _powerDeliverySummary = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isNotElevated = !Elevation.IsProcessElevated();
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _hasDevices;
    [ObservableProperty] private bool _isAdvancedMode;
    [ObservableProperty] private string _searchText = string.Empty;

    // Per-tab visibility (data-gated), recomputed on selection.
    [ObservableProperty] private bool _hasUsbCData;
    [ObservableProperty] private bool _hasUsb4Data;
    [ObservableProperty] private bool _hasAlsoHereData;

    partial void OnIsAdvancedModeChanged(bool value)
    {
        RebuildTree();
        RefreshDetails();
    }

    partial void OnSearchTextChanged(string value) => RebuildTree();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusSummary = "Scanning USB devices…";

        UsbSnapshot snapshot = await Task.Run(() => new UsbInspectorService().Capture());
        _snapshot = snapshot;

        RebuildTree();

        IsNotElevated = !snapshot.IsElevated;
        StatusSummary = BuildStatusSummary(snapshot);
        PowerDeliverySummary = BuildPowerDeliverySummary(snapshot.PowerDelivery);

        if (RootItems.Count == 0)
        {
            SelectedTitle = "No USB devices found";
            HasSelection = false;
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_snapshot is null)
        {
            StatusSummary = "Nothing to export yet — run a scan first.";
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            SuggestedFileName = $"usb-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}",
        };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        InitializeWithWindow.Initialize(picker, App.WindowHandle);

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(file, SnapshotExporter.ToJson(_snapshot));
        StatusSummary = $"Exported snapshot to {file.Path}";
    }

    [RelayCommand]
    private void RelaunchAsAdmin()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
            });
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            StatusSummary = $"Could not relaunch elevated: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyReport()
    {
        if (_snapshot is null)
        {
            StatusSummary = "Nothing to copy yet — run a scan first.";
            return;
        }

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(SnapshotReport.Build(_snapshot));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        StatusSummary = "Copied a plain-text device summary to the clipboard.";
    }

    [RelayCommand]
    private void ClearLog() => EventLog.Clear();

    /// <summary>Rebuilds the topology tree from the current snapshot, mode and search filter.</summary>
    private void RebuildTree()
    {
        RootItems.Clear();
        if (_snapshot is null)
        {
            HasDevices = false;
            return;
        }

        string filter = SearchText?.Trim() ?? string.Empty;

        foreach (UsbNode controller in _snapshot.HostControllers)
        {
            UsbTreeItem? item = BuildFiltered(controller, filter);
            if (item is not null)
            {
                RootItems.Add(item);
            }
        }

        foreach (UsbNode router in _snapshot.Usb4HostRouters)
        {
            UsbTreeItem? item = BuildFiltered(router, filter);
            if (item is not null)
            {
                RootItems.Add(item);
            }
        }

        AddGroupingViews(_snapshot, filter);

        HasDevices = RootItems.Count > 0;
    }

    /// <summary>
    /// Builds a tree item for a node, keeping only nodes that match the filter or have a matching
    /// descendant. Returns null when nothing in the subtree matches. An empty filter keeps everything.
    /// </summary>
    private static UsbTreeItem? BuildFiltered(UsbNode node, string filter)
    {
        var children = new List<UsbTreeItem>();
        foreach (UsbNode child in node.Children)
        {
            UsbTreeItem? c = BuildFiltered(child, filter);
            if (c is not null)
            {
                children.Add(c);
            }
        }

        bool selfMatches = string.IsNullOrEmpty(filter) || Matches(node, filter);
        if (!selfMatches && children.Count == 0)
        {
            return null;
        }

        return new UsbTreeItem(node, children);
    }

    private static bool Matches(UsbNode node, string filter)
    {
        if (node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string type = UsbDeviceClassifier.FriendlyType(UsbDeviceClassifier.Classify(node));
        if (type.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return node.DeviceDescriptor?.Product?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true
            || node.DeviceDescriptor?.Manufacturer?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Adds the synthetic grouping sections. Simple mode shows a single "Grouped by connection" root
    /// (physical ports preferred, else enclosures). Advanced mode shows both the port and enclosure
    /// groupings separately.
    /// </summary>
    private void AddGroupingViews(UsbSnapshot snapshot, string filter)
    {
        Dictionary<string, UsbNode> byInstance = snapshot.EnumerateAll()
            .Where(n => n.InstanceId is not null)
            .GroupBy(n => n.InstanceId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<PhysicalPortGroup> ports = snapshot.PhysicalPorts;
        IReadOnlyList<PhysicalDeviceGroup> devices = snapshot.PhysicalDevices;

        if (IsAdvancedMode)
        {
            AddPortGroupRoot(ports, byInstance, filter, $"By Physical Port (grouped by connector) — {ports.Count}");
            AddDeviceGroupRoot(devices, byInstance, filter, $"Physical Devices (grouped by ContainerId) — {devices.Count}");
        }
        else if (ports.Count > 0)
        {
            AddPortGroupRoot(ports, byInstance, filter, $"Grouped by connection — {ports.Count} shared port(s)");
        }
        else
        {
            AddDeviceGroupRoot(devices, byInstance, filter, $"Grouped by connection — {devices.Count} device(s)");
        }
    }

    private void AddPortGroupRoot(
        IReadOnlyList<PhysicalPortGroup> portGroups,
        Dictionary<string, UsbNode> byInstance,
        string filter,
        string rootName)
    {
        if (portGroups.Count == 0)
        {
            return;
        }

        var portItems = new List<UsbTreeItem>();
        foreach (PhysicalPortGroup group in portGroups)
        {
            var portNode = new UsbNode
            {
                Kind = UsbNodeKind.PhysicalPort,
                Name = $"{group.Name} — {group.Members.Count} functions ({group.ConfidenceBadge})",
                InstanceId = group.PortKey,
            };

            List<UsbTreeItem> members = BuildMemberItems(group.Members, byInstance, filter);
            if (members.Count == 0 && !string.IsNullOrEmpty(filter) && !group.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            portItems.Add(new UsbTreeItem(portNode, members));
        }

        if (portItems.Count == 0)
        {
            return;
        }

        var root = new UsbNode { Kind = UsbNodeKind.PhysicalPortRoot, Name = rootName };
        RootItems.Add(new UsbTreeItem(root, portItems));
    }

    private void AddDeviceGroupRoot(
        IReadOnlyList<PhysicalDeviceGroup> groups,
        Dictionary<string, UsbNode> byInstance,
        string filter,
        string rootName)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var groupItems = new List<UsbTreeItem>();
        foreach (PhysicalDeviceGroup group in groups)
        {
            var groupNode = new UsbNode
            {
                Kind = UsbNodeKind.PhysicalGroup,
                Name = $"{group.Name} — {group.Members.Count} functions",
                InstanceId = group.ContainerId,
            };

            List<UsbTreeItem> members = BuildMemberItems(group.Members, byInstance, filter);
            if (members.Count == 0 && !string.IsNullOrEmpty(filter) && !group.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            groupItems.Add(new UsbTreeItem(groupNode, members));
        }

        if (groupItems.Count == 0)
        {
            return;
        }

        var root = new UsbNode { Kind = UsbNodeKind.PhysicalGroupRoot, Name = rootName };
        RootItems.Add(new UsbTreeItem(root, groupItems));
    }

    private static List<UsbTreeItem> BuildMemberItems(
        IEnumerable<PhysicalDeviceMember> members,
        Dictionary<string, UsbNode> byInstance,
        string filter)
    {
        var items = new List<UsbTreeItem>();
        foreach (PhysicalDeviceMember member in members)
        {
            UsbNode real = member.InstanceId is not null && byInstance.TryGetValue(member.InstanceId, out UsbNode? found)
                ? found
                : new UsbNode { Kind = member.Kind, Name = member.Name, InstanceId = member.InstanceId };

            if (!string.IsNullOrEmpty(filter) && !Matches(real, filter))
            {
                continue;
            }

            items.Add(new UsbTreeItem(real, Array.Empty<UsbTreeItem>()));
        }

        return items;
    }

    private string BuildStatusSummary(UsbSnapshot snapshot)
    {
        string captured = $"Captured {snapshot.CapturedAtUtc.ToLocalTime():HH:mm:ss}";
        if (IsAdvancedMode)
        {
            return $"{snapshot.HostControllers.Count} controller(s), {snapshot.HubCount} hub(s), " +
                   $"{snapshot.DeviceCount} device(s), {snapshot.Usb4RouterCount} USB4 router(s), " +
                   $"{snapshot.PhysicalDevices.Count} physical group(s), {snapshot.PhysicalPorts.Count} physical port(s).  " +
                   captured;
        }

        int ports = snapshot.PhysicalPorts.Count;
        string portText = ports > 0 ? $" across {ports} shared port(s)" : string.Empty;
        return $"{snapshot.DeviceCount} device(s) connected{portText}.  {captured}";
    }

    partial void OnSelectedItemChanged(UsbTreeItem? value) => RefreshDetails();

    /// <summary>Rebuilds the detail panes for the current selection (also re-run when Advanced toggles).</summary>
    private void RefreshDetails()
    {
        OverviewRows.Clear();
        PnpRows.Clear();
        PowerRows.Clear();
        UsbCRows.Clear();
        Usb4Rows.Clear();
        AlsoHereRows.Clear();

        UsbTreeItem? value = SelectedItem;
        if (value is null)
        {
            DescriptorsText = string.Empty;
            RawText = string.Empty;
            SelectedTitle = "Select a device";
            HasSelection = false;
            HasUsbCData = HasUsb4Data = HasAlsoHereData = false;
            return;
        }

        UsbNode node = value.Node;
        SelectedTitle = node.Name;
        HasSelection = true;

        foreach (DetailRow row in DetailsBuilder.Overview(node)) OverviewRows.Add(row);
        foreach (DetailRow row in DetailsBuilder.Pnp(node)) PnpRows.Add(row);
        foreach (DetailRow row in DetailsBuilder.Power(node)) PowerRows.Add(row);
        foreach (DetailRow row in DetailsBuilder.UsbC(node)) UsbCRows.Add(row);
        foreach (DetailRow row in DetailsBuilder.Usb4(node)) Usb4Rows.Add(row);
        foreach (DetailRow row in DetailsBuilder.AlsoHere(node, _snapshot, IsAdvancedMode)) AlsoHereRows.Add(row);

        HasUsbCData = node.UsbC is { HasAnyData: true };
        HasUsb4Data = node.Usb4 is { HasAnyData: true };
        HasAlsoHereData = ComputeHasAlsoHere(node);

        DescriptorsText = DetailsBuilder.Descriptors(node);
        RawText = DetailsBuilder.Raw(node);
    }

    private bool ComputeHasAlsoHere(UsbNode node)
    {
        if (_snapshot is null)
        {
            return false;
        }

        var port = PhysicalPortGrouper.ComputePortKey(node);
        if (port is not null && _snapshot.EnumerateAll().Any(n => !ReferenceEquals(n, node)
                && PhysicalPortGrouper.ComputePortKey(n) is { } c
                && string.Equals(c.Key, port.Value.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string? containerId = node.Pnp?.ContainerId;
        return UsbSnapshot.IsRealContainerId(containerId)
            && _snapshot.EnumerateAll().Any(n => !ReferenceEquals(n, node)
                && string.Equals(n.Pnp?.ContainerId, containerId, StringComparison.OrdinalIgnoreCase));
    }

    private void OnDeviceChanged(UsbDeviceEvent e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            EventLog.Insert(0, e.Description);
            while (EventLog.Count > 200)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }

            DebounceRefresh();
        });
    }

    private void DebounceRefresh()
    {
        _refreshDebounce?.Cancel();
        _refreshDebounce = new CancellationTokenSource();
        CancellationToken token = _refreshDebounce.Token;

        _ = Task.Delay(800, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (RefreshCommand.CanExecute(null))
                    {
                        RefreshCommand.Execute(null);
                    }
                });
            }
        }, TaskScheduler.Default);
    }

    private static string BuildPowerDeliverySummary(PowerDeliveryInfo? pd)
    {
        if (pd is null || !pd.BatteryPresent)
        {
            return "No battery/charging information available on this system.";
        }

        string watts = pd.ChargeRateWatts is double w ? $"{w:0.0} W" : "n/a";
        return $"Charging state: {pd.ChargingState}.  Charge rate: {watts} (reflects USB-C/PD or AC power).";
    }
}
