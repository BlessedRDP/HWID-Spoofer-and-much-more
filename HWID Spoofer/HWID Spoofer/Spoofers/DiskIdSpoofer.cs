using Microsoft.Win32;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs disk drive serial numbers and identifiers in the SCSI device map registry.
/// </summary>
public static class DiskIdSpoofer
{
    private const string ScsiPath = @"HARDWARE\DEVICEMAP\Scsi";
    private const string DiskEnumPath = @"SYSTEM\CurrentControlSet\Enum\IDE";
    private const string StorEnumPath = @"SYSTEM\CurrentControlSet\Enum\SCSI";

    public static Dictionary<string, string?> GetCurrentIds()
    {
        var result = new Dictionary<string, string?>();

        // SCSI device map
        try
        {
            WalkScsiKeys(Registry.LocalMachine, ScsiPath, result, false);
        }
        catch { }

        // IDE enum
        try
        {
            WalkEnumKeys(Registry.LocalMachine, DiskEnumPath, result, false);
        }
        catch { }

        // SCSI enum
        try
        {
            WalkEnumKeys(Registry.LocalMachine, StorEnumPath, result, false);
        }
        catch { }

        return result;
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("Disk Serial Number Spoofing");

        int count = 0;

        // Spoof SCSI device map entries
        try
        {
            var changes = new Dictionary<string, string?>();
            WalkScsiKeys(Registry.LocalMachine, ScsiPath, changes, true);
            count += changes.Count;
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"SCSI device map spoofing failed: {ex.Message}");
        }

        // Spoof IDE enumeration entries
        try
        {
            var changes = new Dictionary<string, string?>();
            WalkEnumKeys(Registry.LocalMachine, DiskEnumPath, changes, true);
            count += changes.Count;
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"IDE enum spoofing failed: {ex.Message}");
        }

        // Spoof SCSI enumeration entries
        try
        {
            var changes = new Dictionary<string, string?>();
            WalkEnumKeys(Registry.LocalMachine, StorEnumPath, changes, true);
            count += changes.Count;
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"SCSI enum spoofing failed: {ex.Message}");
        }

        if (count > 0)
            HwidUtils.WriteSuccess($"Spoofed {count} disk identifier(s)");
        else
            HwidUtils.WriteInfo("No disk identifiers found to spoof");
    }

    private static void WalkScsiKeys(RegistryKey root, string path, Dictionary<string, string?> result, bool write)
    {
        using var key = root.OpenSubKey(path, write);
        if (key == null) return;

        // Check for target values at this level
        foreach (var valueName in new[] { "SerialNumber", "Identifier", "InquiryData" })
        {
            var val = key.GetValue(valueName)?.ToString();
            if (val != null)
            {
                string fullPath = $@"{path}\{valueName}";
                if (write)
                {
                    var newVal = HwidUtils.RandomSerial(val.Length > 0 ? Math.Min(val.Length, 20) : 20);
                    key.SetValue(valueName, newVal, RegistryValueKind.String);
                    HwidUtils.WriteChange($"SCSI {valueName}", val, newVal);
                    result[fullPath] = newVal;
                }
                else
                {
                    result[fullPath] = val;
                }
            }
        }

        // Recurse into sub-keys
        foreach (var sub in key.GetSubKeyNames())
        {
            WalkScsiKeys(root, $@"{path}\{sub}", result, write);
        }
    }

    private static void WalkEnumKeys(RegistryKey root, string path, Dictionary<string, string?> result, bool write)
    {
        using var key = root.OpenSubKey(path, write);
        if (key == null) return;

        foreach (var sub in key.GetSubKeyNames())
        {
            string subPath = $@"{path}\{sub}";
            using var subKey = root.OpenSubKey(subPath, write);
            if (subKey == null) continue;

            // Look for instance sub-keys
            foreach (var instance in subKey.GetSubKeyNames())
            {
                string instancePath = $@"{subPath}\{instance}";
                using var instanceKey = root.OpenSubKey(instancePath, write);
                if (instanceKey == null) continue;

                var friendlyName = instanceKey.GetValue("FriendlyName")?.ToString() ?? sub;

                foreach (var valueName in new[] { "HardwareID", "CompatibleIDs" })
                {
                    // These are multi-string values — just read for display
                    if (!write)
                    {
                        var multiVal = instanceKey.GetValue(valueName) as string[];
                        if (multiVal != null && multiVal.Length > 0)
                        {
                            result[$"{instancePath}\\{valueName}"] = string.Join("; ", multiVal);
                        }
                    }
                }
            }
        }
    }
}
