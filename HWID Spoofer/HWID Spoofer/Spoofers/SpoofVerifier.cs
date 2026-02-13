using Microsoft.Win32;
using System.Management;
using System.Net.NetworkInformation;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Verifies that spoofing has actually taken effect by cross-checking registry values
/// against live WMI/system queries. Reports a pass/fail status for each identifier.
/// </summary>
public static class SpoofVerifier
{
    private static int _passed;
    private static int _failed;
    private static int _warnings;

    /// <summary>
    /// Runs all verification checks and prints a full report.
    /// </summary>
    public static void RunFullVerification()
    {
        _passed = 0;
        _failed = 0;
        _warnings = 0;

        HwidUtils.WriteHeader("SPOOF VERIFICATION — Full System Check");

        var backup = HwidUtils.LoadBackup();
        bool hasBackup = backup != null && backup.Count > 0;

        if (hasBackup)
        {
            HwidUtils.WriteInfo("Backup found — comparing current values against original values.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ No backup file found. Verification will check registry vs WMI");
            Console.WriteLine("    consistency, but cannot confirm values differ from originals.");
            Console.WriteLine("    Tip: Run Backup BEFORE spoofing for the best verification.");
            Console.ResetColor();
        }

        Console.WriteLine();

        VerifyMachineGuid(backup);
        VerifyComputerName(backup);
        VerifyBios(backup);
        VerifyMacAddresses(backup);
        VerifyProductId(backup);
        VerifyDiskIds(backup);

        // ─── Summary ────────────────────────────────────────────────
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('═', 54));
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  RESULTS: ");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{_passed} PASSED");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"{_failed} FAILED");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{_warnings} WARNINGS");
        Console.WriteLine();
        Console.ResetColor();

        if (_failed == 0 && _warnings == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(@"
  ╔═══════════════════════════════════════════════╗
  ║    ✓ YOUR PC IS FULLY SPOOFED!               ║
  ║    All identifiers verified successfully.     ║
  ╚═══════════════════════════════════════════════╝");
            Console.ResetColor();
        }
        else if (_failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(@"
  ╔═══════════════════════════════════════════════╗
  ║    ~ PC MOSTLY SPOOFED                       ║
  ║    Some items need a reboot to take effect.   ║
  ╚═══════════════════════════════════════════════╝");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"
  ╔═══════════════════════════════════════════════╗
  ║    ✗ SPOOFING INCOMPLETE                     ║
  ║    Some identifiers were NOT changed.         ║
  ║    Try running Spoof All again as Admin.      ║
  ╚═══════════════════════════════════════════════╝");
            Console.ResetColor();
        }
    }

    // ─── Individual checks ──────────────────────────────────────────

    private static void VerifyMachineGuid(Dictionary<string, string?>? backup)
    {
        SectionHeader("Machine GUID");
        var current = MachineGuidSpoofer.GetCurrentGuid();

        if (string.IsNullOrEmpty(current))
        {
            Fail("MachineGuid", "Could not read from registry");
            return;
        }

        // Check if GUID format is valid
        if (!Guid.TryParse(current, out _))
        {
            Fail("MachineGuid", $"Invalid GUID format: {current}");
            return;
        }

        // Compare against backup (original)
        if (backup != null && backup.TryGetValue("MachineGuid", out var original) && original != null)
        {
            if (current.Equals(original, StringComparison.OrdinalIgnoreCase))
                Fail("MachineGuid", $"STILL matches original: {current}");
            else
                Pass("MachineGuid", $"{Truncate(current)} (changed from {Truncate(original)})");
        }
        else
        {
            Pass("MachineGuid", $"{current} (valid, no backup to compare)");
        }
    }

    private static void VerifyComputerName(Dictionary<string, string?>? backup)
    {
        SectionHeader("Computer Name");

        var registryName = ComputerNameSpoofer.GetCurrentName();
        var liveName = Environment.MachineName;

        if (backup != null && backup.TryGetValue("ComputerName", out var original) && original != null)
        {
            if (registryName != null && !registryName.Equals(original, StringComparison.OrdinalIgnoreCase))
                Pass("Registry Name", $"{registryName} (changed from {original})");
            else
                Fail("Registry Name", $"Still matches original: {registryName}");
        }
        else
        {
            Info("Registry Name", $"{registryName} (no backup to compare)");
        }

        // Check if live name matches registry (requires reboot)
        if (registryName != null && !registryName.Equals(liveName, StringComparison.OrdinalIgnoreCase))
            Warn("Live vs Registry", $"Live='{liveName}' ≠ Registry='{registryName}' — REBOOT needed");
        else
            Pass("Live vs Registry", $"Both are '{liveName}' — in sync");
    }

    private static void VerifyBios(Dictionary<string, string?>? backup)
    {
        SectionHeader("BIOS / SMBIOS");

        var currentBios = BiosSpoofer.GetCurrentIds();

        // Cross-check registry vs WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                var wmiSerial = obj["SerialNumber"]?.ToString();
                var wmiManufacturer = obj["Manufacturer"]?.ToString();

                var regManufacturer = currentBios.GetValueOrDefault("BIOS Vendor");

                if (wmiManufacturer != null && regManufacturer != null)
                {
                    if (wmiManufacturer.Equals(regManufacturer, StringComparison.OrdinalIgnoreCase))
                        Pass("BIOS Vendor (WMI=Registry)", $"{wmiManufacturer}");
                    else
                        Warn("BIOS Vendor", $"WMI='{wmiManufacturer}' ≠ Registry='{regManufacturer}' — reboot may sync");
                }

                Info("WMI BIOS Serial", wmiSerial ?? "(empty)");
            }
        }
        catch
        {
            Warn("WMI BIOS Query", "Could not query Win32_BIOS via WMI");
        }

        // Cross-check Win32_BaseBoard
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                var wmiProduct = obj["Product"]?.ToString();
                var regProduct = currentBios.GetValueOrDefault("Baseboard Product");

                if (wmiProduct != null && regProduct != null)
                {
                    if (wmiProduct.Equals(regProduct, StringComparison.OrdinalIgnoreCase))
                        Pass("Baseboard Product (WMI=Registry)", $"{wmiProduct}");
                    else
                        Warn("Baseboard Product", $"WMI='{wmiProduct}' ≠ Registry='{regProduct}' — reboot may sync");
                }
            }
        }
        catch
        {
            Warn("WMI BaseBoard Query", "Could not query Win32_BaseBoard via WMI");
        }

        // Verify changed from backup
        if (backup != null)
        {
            foreach (var kv in currentBios)
            {
                var backupKey = $"BIOS_{kv.Key}";
                if (backup.TryGetValue(backupKey, out var origVal) && origVal != null && kv.Value != null)
                {
                    if (kv.Value.Equals(origVal, StringComparison.OrdinalIgnoreCase))
                        Fail(kv.Key, $"STILL matches original: {Truncate(kv.Value)}");
                    else
                        Pass(kv.Key, $"Changed ✓");
                }
            }
        }
        else
        {
            foreach (var kv in currentBios)
                Info(kv.Key, Truncate(kv.Value) ?? "(empty)");
        }
    }

    private static void VerifyMacAddresses(Dictionary<string, string?>? backup)
    {
        SectionHeader("MAC Addresses");

        var registryMacs = MacAddressSpoofer.GetCurrentMacs();

        // Get live MAC addresses from .NET NetworkInterface
        var liveInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                     && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

        foreach (var kv in registryMacs)
        {
            var parts = kv.Key.Split('|');
            var label = parts.Length > 1 ? parts[1] : parts[0];

            if (backup != null && backup.TryGetValue($"MAC_{kv.Key}", out var origMac) && origMac != null)
            {
                if (kv.Value != null && kv.Value.Equals(origMac, StringComparison.OrdinalIgnoreCase))
                    Fail($"MAC [{label}]", $"STILL matches original: {kv.Value}");
                else
                    Pass($"MAC [{label}]", $"Changed from {Truncate(origMac)} → {kv.Value}");
            }
            else
            {
                Info($"MAC [{label}]", $"{kv.Value} (no backup to compare)");
            }
        }

        // Check locally-administered bit (proper spoofed MACs have bit 1 of first octet set)
        foreach (var kv in registryMacs)
        {
            if (kv.Value != null && kv.Value.Length >= 2)
            {
                if (byte.TryParse(kv.Value[..2], System.Globalization.NumberStyles.HexNumber, null, out var firstByte))
                {
                    if ((firstByte & 0x02) != 0)
                        Pass("MAC Format", "Locally-administered bit set correctly");
                    else
                        Warn("MAC Format", "Locally-administered bit NOT set — some systems may reject this");
                }
            }
        }
    }

    private static void VerifyProductId(Dictionary<string, string?>? backup)
    {
        SectionHeader("Windows Product ID");

        var current = ProductIdSpoofer.GetCurrentIds();

        foreach (var kv in current)
        {
            if (backup != null && backup.TryGetValue($"Product_{kv.Key}", out var origVal) && origVal != null)
            {
                if (kv.Value != null && kv.Value.Equals(origVal, StringComparison.OrdinalIgnoreCase))
                    Fail(kv.Key, $"STILL matches original");
                else
                    Pass(kv.Key, "Changed ✓");
            }
            else
            {
                Info(kv.Key, Truncate(kv.Value) ?? "(empty)");
            }
        }
    }

    private static void VerifyDiskIds(Dictionary<string, string?>? backup)
    {
        SectionHeader("Disk Identifiers");

        var current = DiskIdSpoofer.GetCurrentIds();

        if (current.Count == 0)
        {
            Warn("Disk IDs", "No disk identifiers found in registry");
            return;
        }

        int changed = 0, same = 0;
        foreach (var kv in current)
        {
            if (backup != null && backup.TryGetValue($"Disk_{kv.Key}", out var origVal) && origVal != null)
            {
                if (kv.Value != null && kv.Value.Equals(origVal, StringComparison.OrdinalIgnoreCase))
                    same++;
                else
                    changed++;
            }
        }

        if (backup != null)
        {
            if (same == 0 && changed > 0)
                Pass("Disk Serials", $"All {changed} identifier(s) changed ✓");
            else if (changed > 0 && same > 0)
                Warn("Disk Serials", $"{changed} changed, {same} still match original");
            else if (same > 0 && changed == 0)
                Fail("Disk Serials", $"All {same} identifier(s) still match original");
        }
        else
        {
            Info("Disk Serials", $"{current.Count} identifier(s) found (no backup to compare)");
        }
    }

    // ─── Output helpers ─────────────────────────────────────────────

    private static void SectionHeader(string title)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  ─── {title} ───");
        Console.ResetColor();
    }

    private static void Pass(string label, string detail)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  [PASS] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{label}: ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(detail);
        Console.ResetColor();
        _passed++;
    }

    private static void Fail(string label, string detail)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"  [FAIL] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{label}: ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(detail);
        Console.ResetColor();
        _failed++;
    }

    private static void Warn(string label, string detail)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [WARN] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{label}: ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(detail);
        Console.ResetColor();
        _warnings++;
    }

    private static void Info(string label, string detail)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [INFO] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{label}: ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(detail);
        Console.ResetColor();
    }

    private static string? Truncate(string? val, int max = 36)
    {
        if (val == null) return null;
        return val.Length <= max ? val : val[..(max - 3)] + "...";
    }
}
