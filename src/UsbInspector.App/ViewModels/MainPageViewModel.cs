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
    public ObservableCollection<string> EventLog { get; } = new();

    [ObservableProperty] private UsbTreeItem? _selectedItem;
    [ObservableProperty] private string _descriptorsText = string.Empty;
    [ObservableProperty] private string _rawText = string.Empty;
    [ObservableProperty] private string _selectedTitle = "Select a device";
    [ObservableProperty] private string _statusSummary = "Ready. Click Refresh to scan.";
    [ObservableProperty] private string _powerDeliverySummary = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isNotElevated = !Elevation.IsProcessElevated();
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _hasDevices;

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

        RootItems.Clear();
        foreach (UsbNode controller in snapshot.HostControllers)
        {
            RootItems.Add(new UsbTreeItem(controller));
        }

        foreach (UsbNode router in snapshot.Usb4HostRouters)
        {
            RootItems.Add(new UsbTreeItem(router));
        }

        HasDevices = RootItems.Count > 0;

        IsNotElevated = !snapshot.IsElevated;
        StatusSummary = $"{snapshot.HostControllers.Count} controller(s), {snapshot.HubCount} hub(s), " +
                        $"{snapshot.DeviceCount} device(s), {snapshot.Usb4RouterCount} USB4 router(s).  " +
                        $"Captured {snapshot.CapturedAtUtc.ToLocalTime():HH:mm:ss}";
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
    private void ClearLog() => EventLog.Clear();

    partial void OnSelectedItemChanged(UsbTreeItem? value)
    {
        OverviewRows.Clear();
        PnpRows.Clear();
        PowerRows.Clear();
        UsbCRows.Clear();
        Usb4Rows.Clear();

        if (value is null)
        {
            DescriptorsText = string.Empty;
            RawText = string.Empty;
            SelectedTitle = "Select a device";
            HasSelection = false;
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

        DescriptorsText = DetailsBuilder.Descriptors(node);
        RawText = DetailsBuilder.Raw(node);
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
