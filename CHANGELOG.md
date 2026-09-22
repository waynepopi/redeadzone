# Changelog

All notable changes to Deadzone will be documented in this file.

## [0.1.0] - 2026-09-22

### Initial open-source release

**Application**
- WPF desktop utility targeting Windows 10/11 x64
- Requires administrator privileges (UAC elevation via application manifest)

**Controller input**
- Physical game controller input via SDL3 (ppy.SDL3-CS 2026.722.0)
- Controller selection dropdown listing all connected SDL gamepads
- Preferred controller memory — reconnects to the last used controller on startup or reconnection
- Manual refresh to re-enumerate connected gamepads

**Deadzone processing**
- Independent radial inner deadzone for left and right sticks (0 %–30 %)
- Per-controller settings — each controller remembers its own left and right deadzone values

**Drift analysis**
- 3-second drift sampling with live visualization of raw stick samples
- Automatic suggested deadzone calculation based on observed drift magnitude
- One-click apply of suggested deadzone value

**Live visualization**
- Circular stick visualizer showing raw (unprocessed) and processed positions
- Real-time numeric X/Y readout for both raw and processed stick values

**Virtual output**
- HIDMaestro virtual Xbox Series X|S controller output (HIDMaestro.Core.dll v1.9.0)
- Manual Enable / Disable — virtual output is off by default and must be explicitly started
- Virtual controller uses the `xbox-series-xs-bt` profile

**System integration**
- System tray support — optional minimize to tray; double-click to restore; right-click context menu
- Start with Windows — optional elevated logon task via Windows Task Scheduler
- Start Minimized to Tray — suppresses the main window on `--startup` launch

**Settings**
- All settings persisted to `%LOCALAPPDATA%\Deadzone\settings.json`
- Settings include per-controller deadzone values, preferred controller, tray preferences, and startup options

**Distribution**
- Self-contained Windows x64 installer (Inno Setup 7)
- Self-contained standalone ZIP build
- SHA-256 checksums for all release assets
