using Microsoft.Win32;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs BIOS/SMBIOS related identifiers stored in the registry.
/// </summary>
public static class BiosSpoofer
{
    private const string SystemInfoPath = @"SYSTEM\CurrentControlSet\Control\SystemInformation";
    private const string HardwareConfigPath = @"SYSTEM\HardwareConfig";
    private const string BiosDataPath = @"HARDWARE\DESCRIPTION\System\BIOS";

    private static readonly (string Value, Func<string> Generator, string Label)[] SystemInfoValues =
    {
        ("SystemManufacturer",     () => RandomManufacturer(),               "System Manufacturer"),
        ("SystemProductName",      () => $"System Product {HwidUtils.RandomSerial(6)}",    "System Product Name"),
        ("SystemVersion",          () => $"v{Random.Shared.Next(1,9)}.{Random.Shared.Next(0,99):D2}", "System Version"),
        ("BIOSVendor",             () => RandomBiosVendor(),                 "BIOS Vendor"),
        ("BIOSVersion",            () => $"{HwidUtils.RandomSerial(4)}.{Random.Shared.Next(100,999)}", "BIOS Version"),
        ("BIOSReleaseDate",        () => HwidUtils.RandomBiosDate(),         "BIOS Date"),
        ("BaseBoardManufacturer",  () => RandomManufacturer(),               "Baseboard Manufacturer"),
        ("BaseBoardProduct",       () => $"BASE-{HwidUtils.RandomSerial(8)}", "Baseboard Product"),
        ("BaseBoardVersion",       () => $"Rev {Random.Shared.Next(1,9)}.0{Random.Shared.Next(0,9)}", "Baseboard Version"),
    };

    public static Dictionary<string, string?> GetCurrentIds()
    {
        var result = new Dictionary<string, string?>();

        foreach (var (valueName, _, label) in SystemInfoValues)
        {
            result[label] = HwidUtils.RegRead(Registry.LocalMachine, SystemInfoPath, valueName)
                         ?? HwidUtils.RegRead(Registry.LocalMachine, BiosDataPath, valueName);
        }

        // Hardware Config last config GUID
        result["HardwareConfig GUID"] = HwidUtils.RegRead(Registry.LocalMachine, HardwareConfigPath, "LastConfig");

        // ComputerHardwareId
        result["ComputerHardwareId"] = HwidUtils.RegRead(Registry.LocalMachine, SystemInfoPath, "ComputerHardwareId");

        return result;
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("BIOS / SMBIOS Spoofing");

        int count = 0;

        foreach (var (valueName, generator, label) in SystemInfoValues)
        {
            var oldVal = HwidUtils.RegRead(Registry.LocalMachine, SystemInfoPath, valueName);
            var newVal = generator();

            if (HwidUtils.RegWrite(Registry.LocalMachine, SystemInfoPath, valueName, newVal))
            {
                HwidUtils.WriteChange(label, oldVal, newVal);
                count++;
            }

            // Also try to write to BIOS data path
            HwidUtils.RegWrite(Registry.LocalMachine, BiosDataPath, valueName, newVal);
        }

        // Spoof ComputerHardwareId
        var oldHwId = HwidUtils.RegRead(Registry.LocalMachine, SystemInfoPath, "ComputerHardwareId");
        var newHwId = $"{{{HwidUtils.RandomGuid()}}}";
        if (HwidUtils.RegWrite(Registry.LocalMachine, SystemInfoPath, "ComputerHardwareId", newHwId))
        {
            HwidUtils.WriteChange("ComputerHardwareId", oldHwId, newHwId);
            count++;
        }

        // Spoof HardwareConfig LastConfig
        var oldConfig = HwidUtils.RegRead(Registry.LocalMachine, HardwareConfigPath, "LastConfig");
        if (oldConfig != null)
        {
            var newConfig = HwidUtils.RandomGuid();
            if (HwidUtils.RegWrite(Registry.LocalMachine, HardwareConfigPath, "LastConfig", newConfig))
            {
                HwidUtils.WriteChange("HardwareConfig GUID", oldConfig, newConfig);

                // Also rename the old config subkey
                try
                {
                    using var hcKey = Registry.LocalMachine.OpenSubKey(HardwareConfigPath);
                    if (hcKey != null)
                    {
                        foreach (var sub in hcKey.GetSubKeyNames())
                        {
                            if (sub.Equals(oldConfig, StringComparison.OrdinalIgnoreCase))
                            {
                                // Copy values from old GUID key to new GUID key
                                CopyRegistryKey(Registry.LocalMachine,
                                    $@"{HardwareConfigPath}\{sub}",
                                    $@"{HardwareConfigPath}\{newConfig}");
                                break;
                            }
                        }
                    }
                }
                catch { /* Non-critical */ }

                count++;
            }
        }

        if (count > 0)
            HwidUtils.WriteSuccess($"Spoofed {count} BIOS identifier(s)");
        else
            HwidUtils.WriteError("No BIOS identifiers could be spoofed");
    }

    private static void CopyRegistryKey(RegistryKey root, string sourcePath, string destPath)
    {
        using var source = root.OpenSubKey(sourcePath);
        if (source == null) return;

        using var dest = root.CreateSubKey(destPath, true);

        foreach (var valueName in source.GetValueNames())
        {
            var val = source.GetValue(valueName);
            var kind = source.GetValueKind(valueName);
            if (val != null)
                dest.SetValue(valueName, val, kind);
        }

        foreach (var sub in source.GetSubKeyNames())
        {
            CopyRegistryKey(root, $@"{sourcePath}\{sub}", $@"{destPath}\{sub}");
        }
    }

    private static string RandomManufacturer()
    {
        string[] manufacturers = { "Dell Inc.", "Lenovo", "HP", "ASUS", "Acer", "MSI", "Gigabyte Technology Co." };
        return manufacturers[Random.Shared.Next(manufacturers.Length)];
    }

    private static string RandomBiosVendor()
    {
        string[] vendors = { "American Megatrends Inc.", "Phoenix Technologies", "Insyde Corp.", "Award Software" };
        return vendors[Random.Shared.Next(vendors.Length)];
    }
}
