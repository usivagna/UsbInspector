using Windows.Devices.Power;
using Windows.System.Power;

namespace UsbInspector.Core.Services;

/// <summary>System-wide charging / USB Power Delivery readout, from the WinRT battery aggregate.</summary>
public sealed class PowerDeliveryInfo
{
    public bool BatteryPresent { get; init; }
    public string? ChargingState { get; init; }
    public double? ChargeRateWatts { get; init; }
    public int? RemainingCapacityMilliwattHours { get; init; }
    public int? FullChargeCapacityMilliwattHours { get; init; }
    public List<string> Notes { get; } = new();
}

/// <summary>
/// Surfaces the charging/PD state the OS exposes. Windows does not expose raw USB-C Power
/// Delivery PDO contracts to user mode, but the negotiated charge rate (which reflects PD
/// charging) is available through the battery aggregate report.
/// </summary>
public static class PowerDeliveryService
{
    public static PowerDeliveryInfo Read()
    {
        try
        {
            BatteryReport report = Battery.AggregateBattery.GetReport();
            bool present = report.Status != BatteryStatus.NotPresent;

            var info = new PowerDeliveryInfo
            {
                BatteryPresent = present,
                ChargingState = report.Status.ToString(),
                ChargeRateWatts = report.ChargeRateInMilliwatts is int mw ? mw / 1000.0 : null,
                RemainingCapacityMilliwattHours = report.RemainingCapacityInMilliwattHours,
                FullChargeCapacityMilliwattHours = report.FullChargeCapacityInMilliwattHours,
            };

            info.Notes.Add("Charge rate reflects negotiated USB-C/PD or AC power where applicable.");
            info.Notes.Add("Raw USB Power Delivery contract (PDO voltage/current) is not exposed to user mode.");
            return info;
        }
        catch (Exception ex)
        {
            var info = new PowerDeliveryInfo { BatteryPresent = false, ChargingState = "Unavailable" };
            info.Notes.Add($"Battery/charging information unavailable: {ex.Message}");
            return info;
        }
    }
}
