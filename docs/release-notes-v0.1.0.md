# BatteryBar v0.1.0

First public release of BatteryBar for Windows 11.

## Highlights

- Battery percentage in the system tray with two icon layouts and configurable status colors.
- Hover panel with live charge/discharge power, estimated time, and separate 1, 5, 10, and 30 minute averages.
- Optional sign-in startup and configurable thresholds and refresh interval.
- Portable executable with no installer, account, telemetry, or third party package dependencies.

## Install

Download `BatteryBar.exe` from this release and run it on Windows 11 with .NET Framework 4.x. The executable is unsigned, so Windows may show a publisher warning. The app may appear in the hidden notification icons area.

Power and time data depend on the battery driver. Build from source with `./build.ps1` if preferred.
