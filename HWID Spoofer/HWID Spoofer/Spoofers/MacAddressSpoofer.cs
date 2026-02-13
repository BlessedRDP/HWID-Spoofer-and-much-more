using Microsoft.Win32;
using System.Diagnostics;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs MAC addresses for all physical network adapters via the registry.
/// </summary>
public static class MacAddressSpoofer
{
    private const string AdapterClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    public static Dictionary<string, string?> GetCurrentMacs()
    {
        var result = new Dictionary<string, string?>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(AdapterClassKey);
            if (classKey == null) return result;

            foreach (var subName in classKey.GetSubKeyNames())
            {
                if (!int.TryParse(subName, out _)) continue;
                using var adapterKey = classKey.OpenSubKey(subName);
                if (adapterKey == null) continue;

                var driverDesc = adapterKey.GetValue("DriverDesc")?.ToString();
                var componentId = adapterKey.GetValue("ComponentId")?.ToString() ?? "";

                // Skip virtual adapters
                if (string.IsNullOrEmpty(driverDesc)) continue;
                if (componentId.Contains("virtual", StringComparison.OrdinalIgnoreCase)) continue;
                if (componentId.Contains("vmware", StringComparison.OrdinalIgnoreCase)) continue;
                if (componentId.Contains("vpn", StringComparison.OrdinalIgnoreCase)) continue;

                var existingMac = adapterKey.GetValue("NetworkAddress")?.ToString();
                var originalMac = adapterKey.GetValue("OriginalNetworkAddress")?.ToString();
                result[$"{subName}|{driverDesc}"] = existingMac ?? originalMac ?? "(unknown)";
            }
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"Error reading MAC addresses: {ex.Message}");
        }
        return result;
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("MAC Address Spoofing");

        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(AdapterClassKey);
            if (classKey == null)
            {
                HwidUtils.WriteError("Cannot open network adapter registry key");
                return;
            }

            int count = 0;
            foreach (var subName in classKey.GetSubKeyNames())
            {
                if (!int.TryParse(subName, out _)) continue;
                using var adapterKey = classKey.OpenSubKey(subName, true);
                if (adapterKey == null) continue;

                var driverDesc = adapterKey.GetValue("DriverDesc")?.ToString();
                var componentId = adapterKey.GetValue("ComponentId")?.ToString() ?? "";

                if (string.IsNullOrEmpty(driverDesc)) continue;
                if (componentId.Contains("virtual", StringComparison.OrdinalIgnoreCase)) continue;
                if (componentId.Contains("vmware", StringComparison.OrdinalIgnoreCase)) continue;
                if (componentId.Contains("vpn", StringComparison.OrdinalIgnoreCase)) continue;

                var oldMac = adapterKey.GetValue("NetworkAddress")?.ToString()
                    ?? adapterKey.GetValue("OriginalNetworkAddress")?.ToString()
                    ?? "(unknown)";

                var newMac = HwidUtils.RandomMac();
                adapterKey.SetValue("NetworkAddress", newMac, RegistryValueKind.String);

                HwidUtils.WriteChange(driverDesc, oldMac, newMac);
                count++;
            }

            if (count > 0)
            {
                HwidUtils.WriteInfo("Restarting network adapters to apply changes...");
                RestartAdapters();
                HwidUtils.WriteSuccess($"Spoofed {count} adapter(s)");
            }
            else
            {
                HwidUtils.WriteInfo("No physical adapters found to spoof");
            }
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"MAC spoofing failed: {ex.Message}");
        }
    }

    private static void RestartAdapters()
    {
        try
        {
            // Disable and re-enable all adapters to force MAC reload
            RunNetsh("interface set interface * admin=disable");
            Thread.Sleep(2000);
            RunNetsh("interface set interface * admin=enable");
            Thread.Sleep(2000);
        }
        catch
        {
            HwidUtils.WriteInfo("Could not restart adapters automatically. You may need to disable/enable manually or reboot.");
        }
    }

    private static void RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
        }
        catch { /* Silent fail — user will reboot anyway */ }
    }
}
