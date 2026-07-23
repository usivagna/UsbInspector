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
  When run **as administrator**, it additionally consumes the **USB4 connection‑manager ETW
  TraceLogging rundown** — the same data source as the Windows **Settings ▸ USB ▸ USB4 hubs and
  devices** page — to surface full fabric detail: **Domain ID, 7‑byte Topology ID, silicon
  vendor/product/revision, USB4 version, DP‑IN adapter counts, device vendor/model, unit
  vendor/product, firmware version and computed link bandwidth** (e.g. "40Gbps/40Gbps, Gen 3, dual
  lane"), and builds a clean **Host Router → Device Router** topology. Providers:
  `Microsoft.Windows.USB.USB4.HostRouter` `{575BA31F‑2B45‑58C2‑64FD‑F5DC757B6137}` and
  `Microsoft.Windows.USB.USB4.DeviceRouter` `{AE795D36‑2B11‑5EFB‑C7E0‑5D552BC55D6C}`. Real‑time ETW
  requires elevation; **without admin rights the app falls back to the PnP devnode subtree** and shows
  a clear note. Labels USB4‑class link speeds (20/40 Gbps) and tunnelled USB 3.2 links on devices.
- **Physical grouping** — logically‑separate USB3/USB4 functions that share one physical
  device/enclosure are grouped by Windows **`ContainerId`**. The details pane has a **Related** tab
  listing the other functions of the selected node's physical device, and the topology tree gains a
  synthetic **"Physical Devices"** section clustering every node by ContainerId.
- **By Physical Port** — a second grouping clusters functions that share the same **physical host
  port / connector** (e.g. the USB 3.x and USB4 functions on one USB‑C receptacle). A synthetic **"By
  Physical Port"** tree section and a **Port** details tab list the co‑located functions. The port key
  is derived from the best available signal, in priority order: **USB4 fabric port** (ETW domain +
  topology) → **ACPI `_PLD` connector token** (from `DEVPKEY_Device_LocationPaths`) → **location‑path
  prefix** → **`DEVPKEY_Device_LocationInfo`**. Fidelity is firmware‑dependent and each group is
  **labelled with its source/confidence**. True same‑connector identity via ACPI `_PLD/_UPC` is only
  partially exposed to user mode, so lower‑confidence groupings are marked "approximate".
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
| USB4 fabric detail | **ETW TraceLogging rundown** (`Microsoft.Diagnostics.Tracing.TraceEvent`) — real‑time session on the USB4 HostRouter/DeviceRouter providers; same source as Settings "USB4 hubs and devices". **Requires administrator rights.** |
| Physical grouping | **CfgMgr32** — `DEVPKEY_Device_ContainerId` groups the logically‑separate USB3/USB4 functions of one physical device/enclosure |
| Physical‑port grouping | **CfgMgr32 / ETW** — `DEVPKEY_Device_LocationPaths` (ACPI `_PLD` connector token, location‑path prefix), `DEVPKEY_Device_LocationInfo`, and USB4 fabric port; firmware‑dependent, labelled by confidence |
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
  xHCI host controller in the host‑controller tree (related by ContainerId). **Full USB4 fabric
  detail** (Domain/Topology IDs, silicon IDs, model/firmware, DP‑IN adapters, computed bandwidth, and
  the Host Router → Device Router topology) is read from the **USB4 connection‑manager ETW rundown**,
  which **requires administrator rights**. When not elevated, the app shows a note and keeps the PnP
  devnode subtree as a fallback. The documented rundown event list is non‑exhaustive, so all payload
  fields are captured and mapped defensively.
- **Physical grouping** relies on Windows **`ContainerId`**, which relates the separate devnodes of
  one physical device/enclosure.
- **By Physical Port** grouping approximates same‑*connector* identity using ACPI `_PLD` connector
  tokens / location paths / location info and the USB4 fabric port. ACPI `_PLD/_UPC` is only partially
  exposed to user mode and is firmware‑dependent, so groupings are **labelled with their confidence**
  and degrade gracefully to location‑path/location‑info signals when a `_PLD` token is unavailable.
- On systems with **no USB hardware** (e.g. some cloud VMs) the tree is empty and the status bar
  reports zero controllers — this is expected.

## License

See `LICENSE`.
