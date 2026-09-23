# USB Inspector

A native Windows desktop application that discovers and displays **all obtainable USB
information** on the system using **public Windows APIs**. It shows host controllers, the full
hub/port topology tree, connected devices, complete USB descriptors, PnP/driver properties,
power & speed data, **USB Type‑C** information, **USB4** host routers, and **live hot‑plug events**.

Built with **C# WinUI 3** on **.NET 9** (unpackaged desktop app).

---

## Features

- **Device management** — a connected USB device/hub inventory with device-type icons and a
  selectable front/rear port schematic labelled with the PC manufacturer/model reported by Windows.
  Each selection shows its full controller/hub/port route, manufacturer, model and USB serial,
  plus each associated storage disk's serial, capacity, read-only status and mounted volumes'
  drive letters, labels, file systems and capacities. Unavailable data is explicitly unknown.
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
  device/enclosure or connector are grouped for you. The details pane has a single **"Also here"** tab
  that lists the other functions sharing the selected device's **physical port** first (USB4 fabric
  port / ACPI `_PLD` connector token, shown **✓ Confirmed**), then the other functions of the same
  physical enclosure by Windows **`ContainerId`** (shown **~ Likely**). In Simple mode the topology
  tree adds one synthetic **"Grouped by connection"** section (port groups preferred, else enclosure
  groups); Advanced mode shows the port and device groupings separately. The port key is derived from
  the best available signal, in priority order: **USB4 fabric port** (ETW domain + topology) → **ACPI
  `_PLD` connector token** (from `DEVPKEY_Device_LocationPaths`) → **location‑path prefix** →
  **`DEVPKEY_Device_LocationInfo`**; fidelity is firmware‑dependent and each group is labelled with a
  plain‑language confidence badge.
- **Simple vs Advanced view** — the app opens in a friendly **Simple** mode: device‑type icons and
  plain‑language labels/tooltips, empty tabs hidden, and only the tabs relevant to the selected device
  (Overview, Speed & Power, USB‑C, USB4, "Also here"). Toggle **Advanced** in the toolbar to reveal the
  technical tabs (**Descriptors**, **Driver**, **Raw**) and raw keys — nothing is removed, just
  progressively disclosed. A **search box** filters the tree, and **Copy summary** puts a readable
  plain‑text device report on the clipboard (alongside **Export JSON**).
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
| PC model and USB storage | **WMI** — `Win32_ComputerSystem`, `Win32_DiskDrive` → `Win32_DiskPartition` → `Win32_LogicalDisk` associations; **CfgMgr32** ancestry correlates disks to USB nodes; query-only `IOCTL_DISK_IS_WRITABLE` reports write protection |
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

## Set up your USB port map

1. Open **Device management**. Plug a known device into a socket and select it in the connected
   device list. Compare its serial number and drive letter with the device you intended to use.
2. Choose **Front**, **Rear**, or **Other**, give the socket a label such as “upper left”, and
   select **Save port label**. Repeat for the sockets you use. Unlabelled ports remain under
   **Unassigned / other ports**; the app never guesses physical location from a USB port number.
3. Select any port card (including empty/error ports) to inspect its full route and details.
   Refresh and hot-plug scans update the inventory and clear the previous selection to avoid
   showing a removed drive's details. **Topology & advanced details** retains the original views.
4. To remove a location, choose **Unassigned**, clear the label and save.

Labels are saved per Windows user in `%LOCALAPPDATA%\UsbInspector\port-layout.json`, keyed by
machine, controller and hub route rather than by the attached drive. Changing drives does not
move labels; replacing an identifiable downstream hub does not inherit the old hub's labels.
If controller/hub identity is unavailable, saving labels is disabled. Recheck labels after
hardware or firmware changes. USB 2/3 companion logical ports can represent the same physical
socket; identify and label each observed route yourself.

**This is a user-configured schematic, not an automatically discovered drawing of your PC.**
Windows model information does not include chassis artwork, socket positions, or a reliable
front/rear mapping. Internal devices, internal hubs and external hub ports may all appear.
The front/rear panels show only your assignments, in enumeration order, not a to-scale layout.
Model-specific chassis artwork and automatic physical socket placement are not provided.

Disk matching uses exact PnP ancestry, including UASP/SCSI devices, rather than similar names
or serial numbers. Multi-card readers can expose multiple disks and volumes; each is listed
separately. No media, unformatted disks, volumes without drive letters, unavailable providers,
and access restrictions can leave details unavailable. Read-only status is a query result,
not a write test or a guarantee of file permissions. This panel never formats or copies data.
The view is a snapshot: refresh and recheck identifiers immediately before acting in another app.
JSON exports now include the reported PC model and storage details (including serial numbers);
review them before sharing. User-assigned port labels remain local and are not exported.

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
