using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using UsbInspector.Core.Interop;

namespace UsbInspector.Core.Services;

/// <summary>
/// Consumes the USB4 connection-manager TraceLogging "rundown" — the same data source the Windows
/// Settings "USB4 hubs and devices" page uses. Enabling an ETW session for the host-router and
/// device-router providers makes the drivers emit a full description of the domain
/// (RundownStart → per-router/-port/-adapter events → RundownComplete). Requires administrator rights;
/// degrades to an empty result (with a warning) when not elevated. Never throws.
/// </summary>
public sealed class Usb4RundownReader
{
    // ETW providers documented at
    // learn.microsoft.com/windows-hardware/design/component-guidelines/usb4-tracelogging-rundown-events
    private static readonly Guid HostRouterProvider = new("575BA31F-2B45-58C2-64FD-F5DC757B6137");
    private static readonly Guid DeviceRouterProvider = new("AE795D36-2B11-5EFB-C7E0-5D552BC55D6C");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    public Usb4RundownData Read(List<string> warnings, TimeSpan? timeout = null)
    {
        var data = new Usb4RundownData();

        if (!Elevation.IsProcessElevated())
        {
            data.RequiresElevation = true;
            warnings.Add(
                "USB4 fabric details (Domain/Topology IDs, silicon IDs, model/firmware, bandwidth) require "
                + "administrator rights. Showing PnP data only — use 'Restart as Admin' for full USB4 information.");
            return data;
        }

        string sessionName = "UsbInspectorUsb4_" + Guid.NewGuid().ToString("N");
        TimeSpan wait = timeout ?? DefaultTimeout;

        try
        {
            using var session = new TraceEventSession(sessionName) { StopOnDispose = true };
            using var done = new ManualResetEventSlim(false);
            int rundownCompletes = 0;

            session.Source.Dynamic.All += e =>
            {
                string name = e.EventName;
                if (name.EndsWith("RundownComplete", StringComparison.OrdinalIgnoreCase))
                {
                    if (Interlocked.Increment(ref rundownCompletes) >= 2)
                    {
                        done.Set();
                    }

                    return;
                }

                if (name.EndsWith("RundownStart", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                data.Events.Add(Capture(e));
            };

            session.EnableProvider(HostRouterProvider);
            session.EnableProvider(DeviceRouterProvider);

            var processing = Task.Run(() =>
            {
                try
                {
                    session.Source.Process();
                }
                catch
                {
                    // Session stopped/disposed — expected on completion.
                }
            });

            done.Wait(wait);
            session.Source.StopProcessing();
            processing.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            warnings.Add($"USB4 ETW rundown failed: {ex.Message}");
        }

        return data;
    }

    private static Usb4Event Capture(TraceEvent e)
    {
        var evt = new Usb4Event { EventName = e.EventName };

        foreach (string field in e.PayloadNames)
        {
            object? value = SafePayload(e, field);
            if (value is null)
            {
                continue;
            }

            if (field.Equals("TopologyID", StringComparison.OrdinalIgnoreCase) && value is byte[] topo)
            {
                evt.TopologyId = topo;
                evt.Fields[field] = Usb4Fabric.FormatTopologyId(topo);
                continue;
            }

            if (field.Equals("DomainID", StringComparison.OrdinalIgnoreCase))
            {
                evt.DomainId = ToUInt32(value);
            }

            if (field.Equals("DeviceInstancePath", StringComparison.OrdinalIgnoreCase))
            {
                evt.DeviceInstancePath = value.ToString();
            }

            evt.Fields[field] = value is byte[] bytes ? Convert.ToHexString(bytes) : value.ToString() ?? string.Empty;
        }

        return evt;
    }

    private static object? SafePayload(TraceEvent e, string field)
    {
        try
        {
            return e.PayloadByName(field);
        }
        catch
        {
            return null;
        }
    }

    private static uint? ToUInt32(object value) => value switch
    {
        uint u => u,
        int i => (uint)i,
        ushort us => us,
        short s => (uint)s,
        byte b => b,
        long l => (uint)l,
        ulong ul => (uint)ul,
        _ => uint.TryParse(value.ToString(), out uint parsed) ? parsed : null,
    };
}
