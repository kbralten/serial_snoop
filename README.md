# Serial Snoop (Windows)

Serial Snoop is a Windows WPF application that monitors serial traffic by sitting transparently between two COM ports. It is not a terminal emulator; it relays bytes bidirectionally and logs what passes through.

## How It Works

Windows user-mode apps cannot tap an already-open COM port directly. Instead, Serial Snoop works as a proxy:

- The target application connects to an "Upstream" (virtual) COM port.
- Serial Snoop forwards to the "Downstream" (real/physical) COM port.
- All bytes are relayed both ways and logged with timestamp, direction, hex, and ASCII.

To create the virtual COM port pair, use a virtual serial port driver such as com0com (or hub4com). These tools create a paired set of COM ports connected to each other.

## Setup Steps (Virtual Port Pair)

1. Install a signed build of a virtual COM port pair driver (e.g., com0com). Administrator rights are required.
2. Create a port pair, for example `COMA` <-> `COMB`.
3. Configure your target application to use `COMA` as its serial port.
4. In Serial Snoop, set:
   - Upstream: `COMA`
   - Downstream: your real device port (e.g., `COM3`)
   - Matching serial settings (baud rate, data bits, parity, stop bits, handshake) for both ports
5. Click Start. Data flowing between the app and the device will appear in the log.

Notes:
- If your device is a USB-to-serial adapter, unplugging it will stop the bridge; click Stop, reconnect the device, then Start again.
- If you only see RX or TX, verify baud and flow-control settings.

## Build and Run

Requires .NET 8 SDK on Windows.

Build:

```powershell
# From the project root
dotnet restore
dotnet build -c Release
```

Run:

```powershell
# Debug run
dotnet run -c Debug
```

Publish (single-file, Windows x64):

```powershell
dotnet publish .\SerialSnoop.Wpf.csproj -c Release -p:PublishProfile=Properties\PublishProfiles\SerialSnoopSingleFile.pubxml
```

## Creating COM-style Virtual Ports with com0com

If your target application only lists ports named like `COM10`, you can make com0com create or rename virtual pairs with COM-style names. These steps assume you have a com0com installation that includes the command-line utility (often `setupc.exe` or `setup.exe`). Run the commands from an elevated (Administrator) PowerShell or CMD prompt.

1. Open an Administrator PowerShell and change to the com0com install directory (adjust path as needed):

```powershell
cd "C:\Program Files\com0com"
```

2. Create a new pair with explicit COM names (example `COM10` <-> `COM11`):

```powershell
.\setupc.exe install PortName=COM10 PortName=COM11
```

3. Rename an existing pair (if your com0com build supports `change`):

```powershell
.\setupc.exe list                  # find the pair index (e.g. 0)
.\setupc.exe change 0 PortName=COM10 PortName=COM11
```

4. If `change` is not available, remove and re-create the pair:

```powershell
.\setupc.exe remove 0
.\setupc.exe install PortName=COM10 PortName=COM11
```

5. Verify the ports exist:

- In Device Manager → "Ports (COM & LPT)" you should now see `COM10` and `COM11`.
- Or in PowerShell:

```powershell
[System.IO.Ports.SerialPort]::GetPortNames()
```

Notes and gotchas:
- You must run the com0com setup utility as Administrator.
- Different com0com builds ship with slightly different utilities or command names; look in the com0com folder for `setupc.exe`, `setup.exe`, or similar.
- Some legacy applications only show COM1–COM9; pick a low-numbered available COM (e.g., COM4) if needed, but avoid conflicts with real hardware ports.
- If the new names do not immediately appear in other applications, try unplugging/replugging any related devices, restarting the com0com service, or rebooting.

If you'd like, I can add a small diagnostics button in the UI that attempts to open a range of COMx ports and reports which ones are available — that can help find an acceptable COM number for legacy apps.

You can also open the solution in Visual Studio or run under the debugger from VS Code with the C# extension.

## Features

- Bidirectional relay (Upstream ⇄ Downstream) with async non-blocking I/O
- Timestamped log entries with direction (TX/RX), length, hex, and ASCII
- Configurable serial settings (baud, data bits, parity, stop bits, handshake)
- DTR/RTS toggles
- Auto-scroll toggle, Clear Log, Save Log
- Ring-buffered UI to keep the last ~10,000 entries for responsiveness

## Mirroring (Optional)

Serial Snoop can optionally mirror all bytes that pass through the bridge to a third serial port. The "Mirror Port (Opt)" field in the main UI lets you supply a COM port that will receive copies of the raw bytes seen on both the Upstream and Downstream links.

How it works:
- When enabled, each read from either side is copied (best-effort) into a bounded mirror queue; a background writer drains that queue and writes the bytes to the mirror port.
- The mirror port therefore sees the same byte streams from both directions (interleaved) — it is not a directional tap by default.

Usage notes and caveats:
- Configure the mirror port with the same serial settings (baud, parity, data bits, stop bits, handshake) as the bridge so the mirrored bytes are intelligible to the receiver.
- The mirror writer is best-effort: writes time out after a short period (to avoid hanging the bridge) and mirror write errors are ignored so the main bridge keeps running.
- The mirror queue is bounded; if it becomes full additional mirror copies are dropped. The primary Upstream⇄Downstream relay is unaffected by mirror drops.
- Do not select the same COM port for Upstream, Downstream, and Mirror — the UI will prevent identical selections.

Common use-cases:
- Feed a hardware logger or analyzer on a separate serial port.
- Send a copy of all traffic to a second tool or device for passive recording.

Diagnostics:
- Mirror-related errors are intentionally ignored in normal operation; check `logs/diagnostics.log` and `logs/crash.log` if you suspect mirror failures or unexpected behavior.

## Limitations

- This program does not attach to an already open COM port; it must be placed in the middle using a virtual pair.
- Creating virtual COM ports requires a kernel-mode driver (e.g., com0com) and administrator rights to install.

## Troubleshooting

- Access denied when opening a port: ensure no other program has the same port open.
- No data shown: check wiring/ports, verify baud/handshake settings match your device/app, and confirm the target app is connected to the Upstream port.
- Device disconnects during capture: click Stop, reconnect the device, then Start again.
