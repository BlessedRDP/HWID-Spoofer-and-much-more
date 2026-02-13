using Microsoft.Win32;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs the Windows Product ID, Install Date, Build GUID, and Digital Product ID.
/// </summary>
public static class ProductIdSpoofer
{
    private const string WinNTPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private static readonly string[] StringValues = { "ProductId", "BuildGUID", "BuildLab", "BuildLabEx", "EditionID" };

    public static Dictionary<string, string?> GetCurrentIds()
    {
        var result = new Dictionary<string, string?>();

        foreach (var name in StringValues)
        {
            result[name] = HwidUtils.RegRead(Registry.LocalMachine, WinNTPath, name);
        }

        // InstallDate (DWORD - Unix timestamp)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinNTPath);
            var installDate = key?.GetValue("InstallDate");
            if (installDate is int unixTs)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(unixTs).LocalDateTime;
                result["InstallDate"] = $"{unixTs} ({dt:yyyy-MM-dd HH:mm:ss})";
            }
        }
        catch { }

        return result;
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("Windows Product ID Spoofing");

        int count = 0;

        // ProductId
        var oldPid = HwidUtils.RegRead(Registry.LocalMachine, WinNTPath, "ProductId");
        var newPid = HwidUtils.RandomProductId();
        if (HwidUtils.RegWrite(Registry.LocalMachine, WinNTPath, "ProductId", newPid))
        {
            HwidUtils.WriteChange("ProductId", oldPid, newPid);
            count++;
        }

        // BuildGUID
        var oldBuild = HwidUtils.RegRead(Registry.LocalMachine, WinNTPath, "BuildGUID");
        var newBuild = HwidUtils.RandomGuid();
        if (HwidUtils.RegWrite(Registry.LocalMachine, WinNTPath, "BuildGUID", newBuild))
        {
            HwidUtils.WriteChange("BuildGUID", oldBuild, newBuild);
            count++;
        }

        // InstallDate — randomize to a recent date
        try
        {
            var newDate = DateTimeOffset.UtcNow.AddDays(-Random.Shared.Next(30, 365)).ToUnixTimeSeconds();
            if (HwidUtils.RegWrite(Registry.LocalMachine, WinNTPath, "InstallDate", (int)newDate, RegistryValueKind.DWord))
            {
                var oldInstall = HwidUtils.RegRead(Registry.LocalMachine, WinNTPath, "InstallDate");
                var dt = DateTimeOffset.FromUnixTimeSeconds(newDate).LocalDateTime;
                HwidUtils.WriteChange("InstallDate", oldInstall, $"{newDate} ({dt:yyyy-MM-dd})");
                count++;
            }
        }
        catch { }

        // DigitalProductId — randomize the binary blob
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(WinNTPath, true);
            if (key != null)
            {
                var oldDpid = key.GetValue("DigitalProductId") as byte[];
                if (oldDpid != null)
                {
                    byte[] newDpid = new byte[oldDpid.Length];
                    Random.Shared.NextBytes(newDpid);
                    // Preserve the header structure (first 4 bytes = size)
                    Buffer.BlockCopy(oldDpid, 0, newDpid, 0, Math.Min(4, oldDpid.Length));
                    key.SetValue("DigitalProductId", newDpid, RegistryValueKind.Binary);
                    HwidUtils.WriteChange("DigitalProductId", $"[{oldDpid.Length} bytes]", $"[{newDpid.Length} bytes randomized]");
                    count++;
                }

                var oldDpid4 = key.GetValue("DigitalProductId4") as byte[];
                if (oldDpid4 != null)
                {
                    byte[] newDpid4 = new byte[oldDpid4.Length];
                    Random.Shared.NextBytes(newDpid4);
                    Buffer.BlockCopy(oldDpid4, 0, newDpid4, 0, Math.Min(4, oldDpid4.Length));
                    key.SetValue("DigitalProductId4", newDpid4, RegistryValueKind.Binary);
                    HwidUtils.WriteChange("DigitalProductId4", $"[{oldDpid4.Length} bytes]", $"[{newDpid4.Length} bytes randomized]");
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            HwidUtils.WriteError($"DigitalProductId spoofing failed: {ex.Message}");
        }

        if (count > 0)
            HwidUtils.WriteSuccess($"Spoofed {count} product identifier(s)");
        else
            HwidUtils.WriteError("No product identifiers could be spoofed");
    }
}
