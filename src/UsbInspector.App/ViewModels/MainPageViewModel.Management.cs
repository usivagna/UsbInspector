using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;

namespace UsbInspector_App.ViewModels;

public sealed partial class MainPageViewModel
{
    private readonly PortLayoutStore _portLayoutStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbInspector", "port-layout.json"));
    private Dictionary<string, PortAssignment> _portAssignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PortMapItem> _allPorts = new();

    public ObservableCollection<PortMapItem> ConnectedDevices { get; } = new();
    public ObservableCollection<PortMapItem> FrontPorts { get; } = new();
    public ObservableCollection<PortMapItem> RearPorts { get; } = new();
    public ObservableCollection<PortMapItem> OtherPorts { get; } = new();
    public ObservableCollection<DetailRow> ManagementRows { get; } = new();
    public IReadOnlyList<string> ChassisSides { get; } = Enum.GetNames<ChassisSide>();

    [ObservableProperty] private string _computerTitle = "PC model not reported";
    [ObservableProperty] private string _inventorySummary = "Scanning…";
    [ObservableProperty] private string _portLayoutMessage = string.Empty;
    [ObservableProperty] private string _scanNotes = string.Empty;
    [ObservableProperty] private string _selectedPortRoute = "Select a device or port to inspect it.";
    [ObservableProperty] private string _portLabelDraft = string.Empty;
    [ObservableProperty] private string _selectedChassisSide = nameof(ChassisSide.Unassigned);
    [ObservableProperty] private bool _canLabelPort;
    private PortMapItem? _selectedPort;

    private void InitializeDeviceManagement()
    {
        try
        {
            _portAssignments = _portLayoutStore.Load();
        }
        catch (Exception ex)
        {
            PortLayoutMessage = $"Saved port labels could not be loaded: {ex.Message}";
        }
    }

    private void RebuildDeviceManagement()
    {
        _allPorts.Clear();
        ConnectedDevices.Clear();
        FrontPorts.Clear();
        RearPorts.Clear();
        OtherPorts.Clear();
        if (_snapshot is null) return;

        ComputerTitle = string.Join(" ", new[] { _snapshot.ComputerManufacturer, _snapshot.ComputerModel }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(ComputerTitle)) ComputerTitle = "PC model not reported";
        foreach (PortMapEntry port in PortMapBuilder.Build(_snapshot))
        {
            PortAssignment? assignment = null;
            if (port.Key is not null) _portAssignments.TryGetValue(port.Key, out assignment);
            var item = new PortMapItem(port, assignment);
            _allPorts.Add(item);
            if (port.IsConnected) ConnectedDevices.Add(item);
            switch (item.Side)
            {
                case ChassisSide.Front: FrontPorts.Add(item); break;
                case ChassisSide.Rear: RearPorts.Add(item); break;
                default: OtherPorts.Add(item); break;
            }
        }

        InventorySummary = $"{ConnectedDevices.Count} connected device(s) / hubs · {_allPorts.Count} reported logical ports";
        ScanNotes = string.Join(Environment.NewLine, _snapshot.Warnings);
        RefreshManagementSelection();
    }

    public void SelectManagementPort(PortMapItem item)
    {
        SelectedItem = new UsbTreeItem(item.Node, Array.Empty<UsbTreeItem>());
    }

    private void RefreshManagementSelection()
    {
        ManagementRows.Clear();
        UsbNode? node = SelectedItem?.Node;
        _selectedPort = _allPorts.FirstOrDefault(p => ReferenceEquals(p.Node, node));
        CanLabelPort = _selectedPort?.Entry.Key is not null;
        SelectedPortRoute = _selectedPort?.Route ?? "Select a device or port to inspect it.";
        SelectedChassisSide = (_selectedPort?.Side ?? ChassisSide.Unassigned).ToString();
        PortLabelDraft = _selectedPort?.SavedLabel ?? string.Empty;
        if (node is not null)
        {
            foreach (DetailRow row in DetailsBuilder.Management(node)) ManagementRows.Add(row);
        }
    }

    [RelayCommand]
    private void SavePortLabel()
    {
        if (IsBusy || _selectedPort?.Entry.Key is not string key
            || !Enum.TryParse(SelectedChassisSide, out ChassisSide side) || !Enum.IsDefined(side))
        {
            return;
        }

        var updated = new Dictionary<string, PortAssignment>(_portAssignments, StringComparer.OrdinalIgnoreCase);
        string label = PortLabelDraft.Trim();
        if (label.Length > 80) return;
        if (side == ChassisSide.Unassigned && label.Length == 0) updated.Remove(key);
        else updated[key] = new PortAssignment(side, label);
        try
        {
            _portLayoutStore.Save(updated);
            _portAssignments = updated;
            RebuildDeviceManagement();
            PortLayoutMessage = "Port label saved on this PC. User-assigned location — verify before using a drive.";
        }
        catch (Exception ex)
        {
            PortLayoutMessage = $"Port label could not be saved: {ex.Message}";
        }
    }
}
