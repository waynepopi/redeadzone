# Deadzone

Deadzone is an open-source Windows utility for applying independent radial inner deadzones to physical game controllers before forwarding processed input to a virtual Xbox Series X|S controller.

It reads from a physical controller via SDL3, applies configurable per-stick deadzones, and outputs clean corrected input to a virtual Xbox controller via HIDMaestro — so games see only the processed result.

> **Deadzone is an independent open-source project and is not affiliated with, endorsed by, or sponsored by Microsoft or Xbox.**

---

## Features

- **Radial deadzone correction** — independent left and right stick deadzones (0 %–30 %)
- **Per-controller settings** — each controller remembers its own deadzone values
- **Preferred controller** — reconnects to the last used controller; never silently substitutes another
- **Drift Analyzer** — 3-second sampling with live dot visualizer; calculates a safe suggested deadzone
- **Live visualization** — raw and processed stick positions overlaid on the circular visualizer
- **Real-time numeric readout** — X/Y values for both raw and processed output
- **HIDMaestro virtual controller** — virtual Xbox Series X|S controller output via HIDMaestro
- **Enable / Disable** — start and stop virtual output at any time without restarting
- **System tray** — optional minimize to tray; double-click to restore; right-click for menu
- **Start with Windows** — optional elevated logon task via Task Scheduler
- **Persistent settings** — saved to `%LOCALAPPDATA%\Deadzone\settings.json`

---

## Requirements

- Windows 10 or Windows 11 (64-bit)
- x64 processor
- Administrator privileges (required for virtual controller output)
- A physical game controller supported by SDL3

> The official installer is self-contained. You do **not** need to separately install the .NET 10 Desktop Runtime.

---

## Installation

### Installer (recommended)

1. Download `Deadzone-v0.1.0-Setup.exe` from the [Releases](../../releases) page.
2. Run the installer — UAC will prompt for administrator approval.
3. Deadzone is installed to `C:\Program Files\Deadzone`.
4. A Start Menu shortcut is created automatically.
5. An optional desktop shortcut can be created during setup.

> **Note:** Windows SmartScreen or your browser may warn about an unsigned application because Deadzone v0.1.0 is not yet code-signed with an Authenticode certificate. This is expected for the initial release. You can verify the SHA-256 hash of the installer against `SHA256SUMS.txt` on the releases page.

### ZIP build (standalone)

1. Download `Deadzone-v0.1.0-win-x64.zip` from the [Releases](../../releases) page.
2. Extract to a folder of your choice.
3. Run `Deadzone.App.exe` as Administrator.

> The ZIP build stores settings in `%LOCALAPPDATA%\Deadzone` and is not a fully portable installation.

---

## Using Deadzone

### Controller selection

Use the **Controller** dropdown to select a physical controller from the list of connected SDL gamepads. Click **REFRESH** to re-enumerate connected devices if a controller was plugged in after Deadzone started.

Deadzone remembers your last used controller and reconnects automatically when it is detected again.

### Deadzone adjustment

Use the **Left Stick** and **Right Stick** sliders to set the radial deadzone for each axis independently. The range is 0 %–30 %. Changes take effect immediately and are persisted per controller.

### Drift analysis

1. Connect your controller and leave the stick completely centred.
2. Click **ANALYZE DRIFT** on the Left or Right stick card.
3. Hold the stick still for 3 seconds while samples are collected.
4. The suggested deadzone is displayed. Click **APPLY** to use it.

The suggestion formula: `clamp(ceil((maxDrift + 0.01) × 100) / 100, 0.00, 0.30)`

### Enable / Disable

Click **ENABLE** to start virtual controller output. The virtual Xbox controller becomes visible to games.  
Click **DISABLE** to stop virtual output without closing Deadzone.

Virtual output is **off by default**. You must explicitly press ENABLE.

---

## Preventing double input with HidHide

Without HidHide, games receive input from *both* the physical controller *and* the virtual controller, causing every button press and stick movement to register twice.

HidHide is a kernel-level device-hiding driver that lets you hide the physical controller from all applications except Deadzone.

**Deadzone does not install or configure HidHide automatically.** This is a one-time manual setup.

### Step 1 — Install HidHide

Download from the official repository:

> **https://github.com/nefarius/HidHide/releases**

Run the installer. A system restart may be required.

### Step 2 — Whitelist Deadzone

1. Open **HidHide Configuration Client** from the Start menu.
2. Go to the **Applications** tab.
3. Click **+** and browse to `Deadzone.exe` in your installation folder.
4. Confirm Deadzone appears in the whitelist.

### Step 3 — Hide the physical controller

1. Go to the **Devices** tab.
2. Find your physical controller.
3. Check the checkbox to enable hiding for that device.

### Step 4 — Enable the cloak

On the **Devices** tab, ensure the **Enable device hiding** toggle is **ON**.

### Verify

- Deadzone should still detect your controller.
- Open `joy.cpl` — the physical controller should no longer appear.
- Press **ENABLE** in Deadzone — the virtual controller should appear.

### Troubleshooting HidHide

| Problem | Solution |
|---|---|
| Deadzone no longer sees the controller | Ensure `Deadzone.App.exe` is whitelisted in the Applications tab |
| Game still sees both controllers | Confirm device hiding is enabled (cloak toggle is ON) |
| Virtual controller not appearing in game | Press ENABLE in Deadzone; verify HIDMaestro initialised successfully |
| HidHide not installed | Re-run the HidHide installer as Administrator |

---

## Start with Windows

In **Settings** (⚙ button), enable **Start with Windows**. Deadzone creates an elevated Windows Task Scheduler logon task named `Deadzone`.

Enable **Start Minimized to Tray** alongside this option to launch silently on logon.

To remove the startup task, open Settings and disable **Start with Windows**, or uninstall Deadzone.

---

## Building from source

**Prerequisites:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 or 11 x64

```powershell
git clone https://github.com/<your-username>/Deadzone.git
cd Deadzone
dotnet restore
dotnet build Deadzone.sln -c Release
dotnet test Deadzone.sln -c Release
dotnet run --project src/Deadzone.App/Deadzone.App.csproj -c Release
```

---

## Creating an installer

**Prerequisites:**
- [Inno Setup 7](https://jrsoftware.org/isinfo.php) installed at `C:\Program Files\Inno Setup 7`

```powershell
.\scripts\package.ps1 -Version 0.1.0
```

This produces:
```
artifacts/release/
├── Deadzone-v0.1.0-Setup.exe
├── Deadzone-v0.1.0-win-x64.zip
└── SHA256SUMS.txt
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| UAC prompt does not appear | Deadzone requires administrator rights — right-click the EXE and choose **Run as administrator** |
| Controller not detected | Click **REFRESH**; reconnect the controller; check SDL3 compatibility |
| Virtual controller not visible in game | Press **ENABLE** in Deadzone before launching the game |
| Double input in game | See [Preventing double input with HidHide](#preventing-double-input-with-hidhide) |
| Deadzone not starting on logon | Open Settings and re-enable **Start with Windows** |
| Settings lost after reinstall | Settings are preserved in `%LOCALAPPDATA%\Deadzone` — they are not removed by the installer |

---

## Known limitations

- Only one physical controller is processed at a time (selectable via the dropdown).
- Virtual output emulates an Xbox Series X|S controller via HIDMaestro.
- Physical controller hiding is a manual one-time setup through HidHide.
- Some games may behave differently when multiple controllers are visible.
- Guide button and Share button availability depends on the SDL version, driver, and controller model.
- Persistent identity for two physically identical controllers can be ambiguous when the hardware exposes no unique serial information.

---

## Privacy

Deadzone does not connect to the internet and does not transmit any data.

- All settings are stored locally in `%LOCALAPPDATA%\Deadzone\settings.json`.
- No account, login, or registration is required.
- No analytics, telemetry, or crash-reporting services are included.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

Deadzone's source code is licensed under the [MIT License](LICENSE).

---

## Third-party software

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for full details.

| Library | Version | License |
|---|---|---|
| [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) | 1.9.0 | MIT |
| [ppy.SDL3-CS](https://github.com/ppy/SDL3-CS) | 2026.722.0 | MIT |
| [SDL3](https://github.com/libsdl-org/SDL) | (via SDL3-CS) | zlib |
