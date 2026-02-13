using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace HWID_Spoofer;

/// <summary>
/// Debug diagnostics tool that dumps all hardware identifiers from every source
/// (registry, WMI, .NET APIs, Win32) so the user can see exactly what's happening
/// and troubleshoot any spoofing issues.
/// </summary>
public static class DebugDiagnostics
{
    private static readonly StringBuilder Log = new();
    private static int _errorCount;
    private static int _warningCount;

    public static void RunFullDiagnostics()
    {
        Log.Clear();
        _errorCount = 0;
        _warningCount = 0;

        HwidUtils.WriteHeader("DEBUG DIAGNOSTICS — Full System Dump");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Collecting data from Registry, WMI, .NET APIs, and Win32...");
        Console.ResetColor();

        LogSection("SYSTEM ENVIRONMENT");
        LogItem("OS Version", Environment.OSVersion.ToString());
        LogItem("64-bit OS", Environment.Is64BitOperatingSystem.ToString());
        LogItem("64-bit Process", Environment.Is64BitProcess.ToString());
        LogItem("Machine Name (.NET)", Environment.MachineName);
        LogItem("User Name", Environment.UserName);
        LogItem("CLR Version", Environment.Version.ToString());
        LogItem("Admin Privileges", HwidUtils.IsRunningAsAdmin().ToString());
        LogItem("Current Time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        LogItem("System Boot", GetLastBootTime());

        // ─── Registry Reads ─────────────────────────────────────────
        LogSection("REGISTRY — Machine GUID");
        DumpRegValue(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid");

        LogSection("REGISTRY — Computer Name");
        DumpRegValue(@"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName", "ComputerName");
        DumpRegValue(@"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName", "ComputerName");
        DumpRegValue(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Hostname");
        DumpRegValue(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "NV Hostname");

        LogSection("REGISTRY — BIOS / SystemInformation");
        string[] biosValues = { "SystemManufacturer", "SystemProductName", "SystemVersion",
            "BIOSVendor", "BIOSVersion", "BIOSReleaseDate", "BaseBoardManufacturer",
            "BaseBoardProduct", "BaseBoardVersion", "ComputerHardwareId" };
        foreach (var v in biosValues)
            DumpRegValue(@"SYSTEM\CurrentControlSet\Control\SystemInformation", v);

        LogSection("REGISTRY — BIOS HARDWARE\\DESCRIPTION");
        foreach (var v in biosValues)
            DumpRegValue(@"HARDWARE\DESCRIPTION\System\BIOS", v);

        LogSection("REGISTRY — HardwareConfig");
        DumpRegValue(@"SYSTEM\HardwareConfig", "LastConfig");
        try
        {
            using var hcKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\HardwareConfig");
            if (hcKey != null)
            {
                foreach (var sub in hcKey.GetSubKeyNames())
                    LogItem("  SubKey", sub);
            }
        }
        catch (Exception ex) { LogError("HardwareConfig subkeys", ex.Message); }

        LogSection("REGISTRY — Windows Product ID");
        string[] productValues = { "ProductId", "BuildGUID", "BuildLab", "BuildLabEx",
            "EditionID", "CurrentBuild", "InstallDate" };
        foreach (var v in productValues)
            DumpRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", v);

        // Digital Product Id blobs
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var dpid = key.GetValue("DigitalProductId") as byte[];
                LogItem("DigitalProductId", dpid != null ? $"[{dpid.Length} bytes] {BitConverter.ToString(dpid, 0, Math.Min(16, dpid.Length))}..." : "(null)");

                var dpid4 = key.GetValue("DigitalProductId4") as byte[];
                LogItem("DigitalProductId4", dpid4 != null ? $"[{dpid4.Length} bytes] {BitConverter.ToString(dpid4, 0, Math.Min(16, dpid4.Length))}..." : "(null)");
            }
        }
        catch (Exception ex) { LogError("DigitalProductId read", ex.Message); }

        LogSection("REGISTRY — Network Adapters (MAC)");
        DumpNetworkAdapterRegistry();

        LogSection("REGISTRY — SCSI Device Map (Disk Serials)");
        DumpScsiDeviceMap(@"HARDWARE\DEVICEMAP\Scsi", 0);

        // ─── WMI Queries ────────────────────────────────────────────
        LogSection("WMI — Win32_BIOS");
        DumpWmi("SELECT * FROM Win32_BIOS", "SerialNumber", "Manufacturer", "Version", "ReleaseDate", "SMBIOSBIOSVersion");

        LogSection("WMI — Win32_BaseBoard");
        DumpWmi("SELECT * FROM Win32_BaseBoard", "Product", "Manufacturer", "SerialNumber", "Version");

        LogSection("WMI — Win32_ComputerSystem");
        DumpWmi("SELECT * FROM Win32_ComputerSystem", "Name", "Manufacturer", "Model", "SystemType", "TotalPhysicalMemory");

        LogSection("WMI — Win32_ComputerSystemProduct");
        DumpWmi("SELECT * FROM Win32_ComputerSystemProduct", "UUID", "IdentifyingNumber", "Name", "Vendor");

        LogSection("WMI — Win32_DiskDrive");
        DumpWmi("SELECT * FROM Win32_DiskDrive", "SerialNumber", "Model", "InterfaceType", "FirmwareRevision", "PNPDeviceID");

        LogSection("WMI — Win32_NetworkAdapterConfiguration (MAC)");
        DumpWmi("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True", "MACAddress", "Description", "DHCPServer");

        LogSection("WMI — Win32_OperatingSystem");
        DumpWmi("SELECT * FROM Win32_OperatingSystem", "SerialNumber", "InstallDate", "Caption", "Version", "BuildNumber");

        LogSection("WMI — Win32_Processor");
        DumpWmi("SELECT * FROM Win32_Processor", "ProcessorId", "Name", "Manufacturer", "UniqueId");

        // ─── .NET Network Interfaces ────────────────────────────────
        LogSection(".NET — NetworkInterface.GetAllNetworkInterfaces()");
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var mac = nic.GetPhysicalAddress().ToString();
                LogItem($"  {nic.Name}", $"Type={nic.NetworkInterfaceType}, MAC={mac}, Status={nic.OperationalStatus}");
            }
        }
        catch (Exception ex) { LogError(".NET NIC enum", ex.Message); }

        // ─── Consistency Checks ─────────────────────────────────────
        LogSection("CONSISTENCY CHECKS");
        CheckConsistency("Computer Name", 
            Environment.MachineName,
            HwidUtils.RegRead(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName", "ComputerName"),
            "Environment.MachineName", "Registry ActiveComputerName");

        CheckConsistency("Computer Name (pending)", 
            HwidUtils.RegRead(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName", "ComputerName"),
            HwidUtils.RegRead(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName", "ComputerName"),
            "Registry Active", "Registry Pending");

        // ─── Summary & Save ─────────────────────────────────────────
        LogSection("DIAGNOSTIC SUMMARY");
        LogItem("Total Errors", _errorCount.ToString());
        LogItem("Total Warnings", _warningCount.ToString());

        // Save to file
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"hwid_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.WriteAllText(logPath, Log.ToString());

        Console.WriteLine();
        HwidUtils.WriteSuccess($"Full diagnostic log saved to:");
        HwidUtils.WriteInfo(logPath);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  ({Log.Length:N0} characters, {_errorCount} errors, {_warningCount} warnings)");
        Console.ResetColor();

        if (_errorCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  ✗ {_errorCount} error(s) detected — see log for details.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ✓ No errors detected during diagnostics.");
            Console.ResetColor();
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static void DumpRegValue(string path, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key == null)
            {
                LogWarning($"HKLM\\{path}", "Key not found");
                return;
            }

            var val = key.GetValue(valueName);
            if (val == null)
            {
                LogItem($"  {valueName}", "(not set)");
            }
            else if (val is byte[] bytes)
            {
                LogItem($"  {valueName}", $"[Binary, {bytes.Length} bytes] {BitConverter.ToString(bytes, 0, Math.Min(20, bytes.Length))}...");
            }
            else if (val is string[] multi)
            {
                LogItem($"  {valueName}", $"[MultiString] {string.Join("; ", multi)}");
            }
            else
            {
                LogItem($"  {valueName}", val.ToString() ?? "(empty)");
            }
        }
        catch (Exception ex)
        {
            LogError($"Registry {path}\\{valueName}", ex.Message);
        }
    }

    private static void DumpNetworkAdapterRegistry()
    {
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(basePath);
            if (classKey == null) { LogWarning("NIC Registry", "Key not found"); return; }

            foreach (var subName in classKey.GetSubKeyNames())
            {
                if (!int.TryParse(subName, out _)) continue;
                using var key = classKey.OpenSubKey(subName);
                if (key == null) continue;

                var desc = key.GetValue("DriverDesc")?.ToString();
                if (string.IsNullOrEmpty(desc)) continue;

                var networkAddr = key.GetValue("NetworkAddress")?.ToString() ?? "(not set)";
                var originalAddr = key.GetValue("OriginalNetworkAddress")?.ToString() ?? "(not set)";
                var componentId = key.GetValue("ComponentId")?.ToString() ?? "";

                LogItem($"  [{subName}] {desc}", "");
                LogItem($"       NetworkAddress", networkAddr);
                LogItem($"       OriginalNetworkAddress", originalAddr);
                LogItem($"       ComponentId", componentId);
            }
        }
        catch (Exception ex) { LogError("NIC Registry dump", ex.Message); }
    }

    private static void DumpScsiDeviceMap(string path, int depth)
    {
        if (depth > 5) return;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key == null) return;

            var indent = new string(' ', depth * 2 + 4);

            foreach (var valueName in key.GetValueNames())
            {
                var val = key.GetValue(valueName);
                if (val is byte[] bytes)
                    LogItem($"{indent}{valueName}", $"[Binary] {Encoding.ASCII.GetString(bytes).TrimEnd('\0')}");
                else
                    LogItem($"{indent}{valueName}", val?.ToString() ?? "(null)");
            }

            foreach (var sub in key.GetSubKeyNames())
            {
                LogItem($"{indent}[{sub}]", "");
                DumpScsiDeviceMap($@"{path}\{sub}", depth + 1);
            }
        }
        catch (Exception ex) { LogError($"SCSI DeviceMap {path}", ex.Message); }
    }

    private static void DumpWmi(string query, params string[] properties)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            int idx = 0;
            foreach (var obj in searcher.Get())
            {
                if (idx > 0) LogItem("  ---", "");
                foreach (var prop in properties)
                {
                    try
                    {
                        var val = obj[prop]?.ToString();
                        LogItem($"  {prop}", val ?? "(null)");
                    }
                    catch
                    {
                        LogItem($"  {prop}", "(property not available)");
                    }
                }
                idx++;
            }
            if (idx == 0)
                LogItem("  (no results)", "");
        }
        catch (Exception ex)
        {
            LogError($"WMI query: {query[..Math.Min(60, query.Length)]}", ex.Message);
        }
    }

    private static void CheckConsistency(string label, string? value1, string? value2, string source1, string source2)
    {
        if (value1 == null || value2 == null)
        {
            LogWarning($"  {label}", $"Cannot compare — {source1}='{value1}' {source2}='{value2}'");
            return;
        }

        if (value1.Equals(value2, StringComparison.OrdinalIgnoreCase))
            LogItem($"  {label}", $"MATCH ✓ ({source1} = {source2} = '{value1}')");
        else
            LogWarning($"  {label}", $"MISMATCH — {source1}='{value1}' ≠ {source2}='{value2}'");
    }

    private static string GetLastBootTime()
    {
        try
        {
            var uptime = Environment.TickCount64;
            var bootTime = DateTime.Now.AddMilliseconds(-uptime);
            return $"{bootTime:yyyy-MM-dd HH:mm:ss} (uptime: {TimeSpan.FromMilliseconds(uptime):d\\.hh\\:mm\\:ss})";
        }
        catch { return "(unknown)"; }
    }

    // ─── Logging ────────────────────────────────────────────────────

    private static void LogSection(string title)
    {
        var line = $"\n{'=',-54}\n  {title}\n{'=',-54}";
        Log.AppendLine(line);

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  ─── {title} ───");
        Console.ResetColor();
    }

    private static void LogItem(string label, string value)
    {
        var line = $"  {label,-40} {value}";
        Log.AppendLine(line);

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"  {label,-40} ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    private static void LogError(string label, string message)
    {
        var line = $"  [ERROR] {label}: {message}";
        Log.AppendLine(line);

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERROR] {label}: {message}");
        Console.ResetColor();
        _errorCount++;
    }

    private static void LogWarning(string label, string message)
    {
        var line = $"  [WARN]  {label}: {message}";
        Log.AppendLine(line);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [WARN]  {label}: {message}");
        Console.ResetColor();
        _warningCount++;
    }
}
