<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-blueviolet?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" />
  <img src="https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=csharp&logoColor=white" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" />
</p>

<h1 align="center">🖥️ HWID Spoofer</h1>

<p align="center">
  <b>A lightweight, registry-based hardware identity randomizer for Windows.</b><br/>
  Randomize your machine's hardware fingerprint in seconds — no drivers, no reboots to run.
</p>

<p align="center">
  <a href="#-features">Features</a> •
  <a href="#-spoofed-identifiers">Identifiers</a> •
  <a href="#%EF%B8%8F-installation">Installation</a> •
  <a href="#-usage">Usage</a> •
  <a href="#-project-structure">Structure</a> •
  <a href="#%EF%B8%8F-disclaimer">Disclaimer</a>
</p>

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔀 **Spoof All** | Randomize every hardware identifier in one click |
| 🎯 **Spoof Individual** | Target a specific identifier (MAC, Disk, BIOS, etc.) |
| 💾 **Backup & Restore** | Save your original IDs to JSON and restore them anytime |
| ✅ **Spoof Verification** | Cross-check registry values against live WMI/system queries to confirm spoofing took effect |
| 🔧 **Debug Diagnostics** | Full system dump — Registry, WMI, .NET APIs — exported to a timestamped log file |
| 🛡️ **Admin Auto-Elevation** | App manifest requests administrator privileges automatically |

---

## 🆔 Spoofed Identifiers

```
┌──────────────────────────────────────────────────────────────┐
│  Identifier              │  Source / Registry Path           │
├──────────────────────────┼───────────────────────────────────┤
│  Machine GUID            │  HKLM\SOFTWARE\Microsoft\...     │
│  Computer Name           │  HKLM\SYSTEM\...\ComputerName    │
│  BIOS Serial / UUID      │  HKLM\SYSTEM\...\SystemInfo      │
│  SMBIOS Data             │  HKLM\HARDWARE\...\BIOS          │
│  MAC Addresses           │  Network adapter registry keys   │
│  Disk Serial Numbers     │  SCSI device map registry keys   │
│  Windows Product ID      │  HKLM\SOFTWARE\Microsoft\...\NT  │
│  HardwareConfig GUID     │  HKLM\SYSTEM\HardwareConfig      │
└──────────────────────────┴───────────────────────────────────┘
```

---

## ⚙️ Installation

### Prerequisites

- **Windows 10/11** (x64)
- [**.NET 10 SDK**](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- **Administrator privileges** (required for registry writes)

### Build from Source

```bash
git clone https://github.com/YOUR_USERNAME/HWID-Spoofer.git
cd HWID-Spoofer

dotnet build -c Release
```

The compiled binary will be in:
```
HWID Spoofer\bin\Release\net10.0-windows\
```

### Run

```bash
# The app auto-elevates via manifest, but you can also:
dotnet run --project "HWID Spoofer"
```

> [!IMPORTANT]
> Always **run as Administrator**. The application requires elevated privileges to write to protected registry keys.

---

## 🚀 Usage

Launch the application and you'll be greeted with an interactive menu:

```
  ╔═══════════════════════════════════════════════╗
  ║           H W I D   S P O O F E R            ║
  ║         Hardware Identity Randomizer          ║
  ╚═══════════════════════════════════════════════╝

  ┌─────────────────────────────────────────────┐
  │  1.  Show Current Hardware IDs              │
  │  2.  Spoof ALL Identifiers                  │
  │  3.  Spoof Individual Identifier            │
  │  4.  Backup Current IDs to File             │
  │  5.  Restore IDs from Backup                │
  │  ─────────────────────────────────────────  │
  │  6.  ✓ Verify Spoof Status                  │
  │  7.  🔧 Debug Diagnostics                   │
  │  ─────────────────────────────────────────  │
  │  0.  Exit                                   │
  └─────────────────────────────────────────────┘
```

### Recommended Workflow

```
1.  📋  Show Current IDs        — Review your current hardware fingerprint
2.  💾  Backup Current IDs      — Save originals before making changes
3.  🔀  Spoof ALL Identifiers   — Randomize everything
4.  ✅  Verify Spoof Status     — Confirm changes took effect
5.  🔄  Reboot your PC          — Required for all changes to fully apply
```

### Restoring Original IDs

If you need to revert your changes, select **option 5** from the main menu to restore from your backup file (`hwid_backup.json`).

---

## 📁 Project Structure

```
HWID Spoofer/
├── Program.cs                      # Entry point & interactive menu
├── HwidUtils.cs                    # Shared utilities (RNG, registry I/O, console helpers)
├── DebugDiagnostics.cs             # Full system diagnostic dump
├── app.manifest                    # UAC auto-elevation manifest
├── HWID Spoofer.csproj             # .NET 10 project file
│
└── Spoofers/
    ├── BiosSpoofer.cs              # BIOS serial, UUID, SMBIOS data
    ├── ComputerNameSpoofer.cs      # Machine & hostname
    ├── DiskIdSpoofer.cs            # SCSI disk serial numbers
    ├── MacAddressSpoofer.cs        # Network adapter MAC addresses
    ├── MachineGuidSpoofer.cs       # Windows Machine GUID
    ├── ProductIdSpoofer.cs         # Windows Product ID & build info
    └── SpoofVerifier.cs           # Cross-source verification engine
```

---

## 🔍 How It Works

The spoofer operates entirely in **usermode** through the Windows Registry:

1. **Registry Manipulation** — Reads current hardware identifiers from well-known registry paths and overwrites them with cryptographically random values.

2. **WMI Cross-Validation** — The verification engine queries `Win32_BIOS`, `Win32_DiskDrive`, `Win32_NetworkAdapterConfiguration`, and other WMI classes to confirm that spoofed values have propagated.

3. **Consistency Checks** — Compares values across multiple sources (Registry, WMI, .NET APIs) to detect mismatches that could reveal the original hardware fingerprint.

> [!NOTE]
> Changes are applied to the registry immediately but a **system reboot** is required for all modifications to take full effect across the OS.

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| **Runtime** | .NET 10 (Windows) |
| **Language** | C# 13 |
| **WMI Access** | `System.Management` NuGet package |
| **Registry** | `Microsoft.Win32.Registry` |
| **Targeting** | `net10.0-windows` |

---

## ⚠️ Disclaimer

> [!CAUTION]
> This tool is provided **for educational and research purposes only**.
>
> - Modifying hardware identifiers may violate the Terms of Service of certain software or online services.
> - The author is **not responsible** for any misuse, bans, or damages resulting from the use of this tool.
> - Use at your own risk. Always create a backup before spoofing.
> - This tool modifies **Windows Registry values** — incorrect usage could affect system stability.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<p align="center">
  <sub>Built with ❤️ and C#</sub>
</p>
