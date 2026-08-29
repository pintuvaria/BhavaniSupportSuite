# Bhavani Support Suite Pro

**Developer:** Dharmesh Varia | **Organization:** Bhavani Technology

A comprehensive enterprise WPF desktop application for system diagnostics, hardware monitoring, provisioning, networking, security, backup, storage cleanup, and reporting.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![WPF](https://img.shields.io/badge/WPF-8.0-blue)
![License](https://img.shields.io/badge/License-MIT-green)
![Windows](https://img.shields.io/badge/Windows-10%2F11-brightgreen)

---

## Features

| Module | Description |
|--------|-------------|
| **Dashboard** | Real-time CPU, Memory, Disk, Network telemetry with performance gauges |
| **Hardware** | Detailed hardware info (CPU/GPU/RAM/MB/BIOS), SMART status, thermal monitoring |
| **Staging** | System provisioning, driver export, startup configuration |
| **Network** | Subnet scanner, port scanner, IP conflict detection, ARP/MAC lookup |
| **Security** | Process management, startup items, scheduled tasks, unsigned binary detection, BitLocker status |
| **Vault** | Backup & restore with LZMA2 compression, differential backups |
| **Storage** | Deep cleanup (SoftwareDistribution, TEMP, Prefetch, Logs), disk space analysis |
| **Reports** | HTML diagnostic reports, service receipts, RDP/SSH/VNC launcher |

## Technical Stack

- **Framework:** .NET 8 / C# / WPF
- **Architecture:** MVVM (CommunityToolkit.Mvvm)
- **UI:** Custom dark theme (`#181824` background, `#00ADB5` accent)
- **Services:** WMI, Performance Counters, Service Controller
- **Deployment:** Single-file self-contained executable (no .NET runtime required)

## Quick Start

### Download

Download the latest release from [Releases](../../releases) page.

### Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/BhavaniSupportSuite.git
cd BhavaniSupportSuite

# Build
dotnet build -c Release

# Publish single-file executable
dotnet publish -c Release -r win-x64 --self-contained true
```

### Output

```
bin\Release\net8.0-windows\win-x64\publish\BhavaniSupportSuite.exe (67 MB)
```

Simply copy `BhavaniSupportSuite.exe` to any Windows 10/11 machine and run. No installation required.

## Project Structure

```
BhavaniSupportSuite/
├── App.xaml                    # Application entry point
├── MainWindow.xaml             # Main window with sidebar navigation
├── Core/
│   └── ViewModelBase.cs        # MVVM base class
├── Services/
│   ├── DiagnosticsService.cs   # System diagnostics & CLI runner
│   ├── HardwareService.cs      # Hardware monitoring (WMI)
│   ├── NetworkScanner.cs       # Subnet/port scanner
│   ├── SecurityService.cs      # Security audit tools
│   ├── StorageService.cs       # Disk cleanup utilities
│   └── ReportsService.cs       # Report generation
├── ViewModels/
│   ├── MainViewModel.cs        # Navigation & telemetry
│   ├── DashboardViewModel.cs
│   ├── HardwareViewModel.cs
│   ├── StagingViewModel.cs
│   ├── NetworkViewModel.cs
│   ├── SecurityViewModel.cs
│   ├── VaultViewModel.cs
│   ├── StorageViewModel.cs
│   └── ReportsViewModel.cs
├── Views/
│   ├── DashboardView.xaml
│   ├── HardwareView.xaml
│   ├── StagingView.xaml
│   ├── NetworkView.xaml
│   ├── SecurityView.xaml
│   ├── VaultView.xaml
│   ├── StorageView.xaml
│   └── ReportsView.xaml
├── Themes/
│   └── DarkTheme.xaml          # Complete dark UI theme
├── Resources/
│   ├── AppIcon.ico             # Application icon
│   └── AppIcon.svg             # Icon source
└── app.manifest                # Administrator elevation
```

## Requirements

- **OS:** Windows 10 (1809+) or Windows 11 (x64)
- **Privileges:** Runs as Administrator (required for system operations)
- **No dependencies:** Self-contained, no .NET runtime installation needed

## Branding

All reports and outputs are branded:

> **Bhavani Technology** | Prepared by Lead Engineer: **Dharmesh Varia**

## License

Copyright © 2026 Bhavani Technology. All rights reserved.
