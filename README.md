# USB Inspector

A native Windows desktop application that discovers and displays **all obtainable USB
information** on the system using **public Windows APIs**. It shows host controllers, the full
hub/port topology tree, connected devices, complete USB descriptors, PnP/driver properties,
power & speed data, **USB Type‑C** information, **USB4** host routers, and **live hot‑plug events**.

Built with **C# WinUI 3** on **.NET 9** (unpackaged desktop app).

---

## Features

- **Topology tree** — USB host controllers → root hubs → hubs/ports → devices, with empty‑port
  visibility and a warning badge on nodes with issues.
- **Descriptors** — Device, Configuration, Interface, Endpoint, String (localized), and
  **BOS / SuperSpeed(+)** capability descriptors, fully parsed.
- **PnP & Driver** — manufacturer, driver key/version/date/provider, ContainerId, hardware &
  compatible IDs, location, PDO name, problem codes, plus a **Raw** view listing *every* DEVPKEY
  property retrieved from the devnode.
- **Power & Speed** — negotiated link speed (Low/Full/High/SuperSpeed/SuperSpeed+), required
  current (mA), self‑powered / remote‑wakeup flags, connection status.
- **USB‑C / Type‑C** — Gen/lane operating & capability flags (from
  `USB_NODE_CONNECTION_INFORMATION_EX_V2`), **Billboard** alternate modes (e.g. DisplayPort SVIDs),
  SuperSpeedPlus capability, and system charging state / negotiated charge rate (USB‑PD) via the
  WinRT battery aggregate. Fields Windows does not expose to user mode are clearly labelled.
- **USB4** — discovers **USB4 Host Router** devnodes (shown as a top‑level section in the topology
  tree) and **enumerates each router's PnP/PCIe devnode subtree** (tunnelled xHCI host controllers,
  PCIe/DisplayPort tunnels and their descendants) via CfgMgr32, so a router is no longer a flat node.
  Labels USB4‑class link speeds (20/40 Gbps) and tunnelled USB 3.2 links on devices. Full USB4 fabric
  detail (router/adapter topology, PCIe/DisplayPort tunnelling, per‑lane negotiation) is not exposed
  to user mode by public Windows APIs and is clearly labelled as such.
- **Physical grouping** — logically‑separate USB3/USB4 functions that share one physical
  device/enclosure are grouped by Windows **`ContainerId`**. The details pane has a **Related** tab
  listing the other functions of the selected node's physical device, and the topology tree gains a
  synthetic **"Physical Devices"** section clustering every node by ContainerId. True same‑connector
  identity (ACPI `_PLD/_UPC`) is not exposed to user mode.
- **Live events** — real‑time USB arrival/removal log via `CM_Register_Notification`, with a
  debounced auto‑refresh.
- **Export** — save the full snapshot to **JSON** for diagnostics/sharing.

## Public Windows APIs used

| Area | API |
| --- | --- |
| Device/interface enumeration | **SetupAPI** — `SetupDiGetClassDevs`, `SetupDiEnumDeviceInterfaces`, `SetupDiEnumDeviceInfo`, `SetupDiGetDeviceInterfaceDetail` |
| Device tree & properties | **CfgMgr32** — `CM_Get_Parent/Child/Sibling`, `CM_Get_Device_ID`, `CM_Get_DevNode_Property`, `CM_Get_DevNode_Status` |
| Hub/port & descriptor queries | **usbioctl / DeviceIoControl** — `IOCTL_USB_GET_ROOT_HUB_NAME`, `..._GET_NODE_INFORMATION`, `..._GET_NODE_CONNECTION_INFORMATION_EX(_V2)`, `..._GET_DESCRIPTOR_FROM_NODE_CONNECTION`, `..._GET_NODE_CONNECTION_NAME`, `..._GET_HUB_INFORMATION_EX` |
| Supplementary data | **WMI** — `Win32_USBController`, `Win32_USBHub`, `Win32_PnPEntity` |
| Live events | **CfgMgr32** — `CM_Register_Notification` |
| USB4 host routers | **SetupAPI / CfgMgr32** — present‑devnode scan (`SetupDiEnumDeviceInfo`) matched by service/class/description, then subtree walk via `CM_Get_Child`/`CM_Get_Sibling`; DEVPKEY properties (Windows publishes no device‑interface GUID for USB4 host routers) |
| Physical grouping | **CfgMgr32** — `DEVPKEY_Device_ContainerId` groups the logically‑separate USB3/USB4 functions of one physical device/enclosure |
| Charging / USB‑PD | **WinRT** — `Windows.Devices.Power.Battery` |

Native interop is generated with **CsWin32** (`Microsoft.Windows.CsWin32`) from the Win32
metadata; USB IOCTL control codes not present in the metadata are computed from the classic
`CTL_CODE` macro in `Interop/UsbIoctl.cs`.

## Project layout

```
UsbInspector.sln
 ├─ src/UsbInspector.Core        Class library — all interop, services and models (no UI)
 │   ├─ Interop/                 CsWin32 config + native helpers (IOCTLs, device IO, DEVPKEYs, CfgMgr)
 │   ├─ Models/                  UsbNode, descriptors, PnP props, USB‑C, USB4, snapshot
 │   └─ Services/                Enumeration, topology walk, descriptor reading, WMI, watcher, export
 ├─ src/UsbInspector.App         WinUI 3 app (MVVM) — tree, tabbed details, event log, export
 └─ tests/UsbInspector.Core.Tests  xUnit interop / pipeline smoke tests
```

## Requirements

- Windows 10 version 1809 (10.0.17763) or later
- .NET 9 SDK
- The **Windows App SDK 1.x** runtime (installed automatically with Visual Studio; for a clean
  machine, install the [Windows App Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads))

## Build & run

```powershell
# from the repository root
dotnet build UsbInspector.sln -c Debug -p:Platform=x64

# run the app
dotnet run --project src/UsbInspector.App -c Debug -p:Platform=x64
# or launch the built exe:
# src/UsbInspector.App/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/UsbInspector.App.exe
```

Run the tests:

```powershell
dotnet test tests/UsbInspector.Core.Tests -c Debug -p:Platform=x64
```

## Administrator rights

The app runs `asInvoker` (no forced elevation). Some device/configuration **descriptor** IOCTLs
require the hub to be opened with write access, which may need elevation. When not elevated, the
app shows an info banner and a **“Restart as Admin”** button; enumeration, topology, PnP
properties and WMI data still work, but a few descriptors may be unavailable. For the most
complete data, run as Administrator.

## Notes & limitations

- USB **Power Delivery** contract details (raw PDO voltage/current objects) and the physical
  connector type (Type‑C vs legacy) are **not** exposed to user mode by public Windows APIs; the
  app surfaces what is available (negotiated speed, alt modes, charge rate) and labels the rest.
- **USB4** host routers are located by scanning present devnodes (Windows publishes no
  device‑interface GUID for them); their children are the **PnP/PCIe devnode subtree** (a PnP view,
  not a USB hub/port tree). The tunnelled USB devices themselves also appear under the tunnelled
  xHCI host controller in the host‑controller tree (related by ContainerId). Full USB4 fabric detail
  — router/adapter topology, PCIe and DisplayPort tunnelling, and per‑lane link negotiation — is
  **not** exposed to user mode and is labelled accordingly.
- **Physical grouping** relies on Windows **`ContainerId`**, which relates the separate devnodes of
  one physical device/enclosure. True per‑*connector* identity (ACPI `_PLD/_UPC`) is **not** cleanly
  exposed to user mode, so grouping is at device/enclosure granularity.
- On systems with **no USB hardware** (e.g. some cloud VMs) the tree is empty and the status bar
  reports zero controllers — this is expected.

## License

See `LICENSE`.
