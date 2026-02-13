using Microsoft.Win32;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs the Windows Machine GUID used for licensing, telemetry, and fingerprinting.
/// </summary>
public static class MachineGuidSpoofer
{
    private const string CryptoPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string ValueName = "MachineGuid";

    public static string? GetCurrentGuid()
    {
        return HwidUtils.RegRead(Registry.LocalMachine, CryptoPath, ValueName);
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("Machine GUID Spoofing");

        var oldGuid = GetCurrentGuid();
        var newGuid = HwidUtils.RandomGuid();

        if (HwidUtils.RegWrite(Registry.LocalMachine, CryptoPath, ValueName, newGuid))
        {
            HwidUtils.WriteChange("MachineGuid", oldGuid, newGuid);
            HwidUtils.WriteSuccess("Machine GUID spoofed");
        }
        else
        {
            HwidUtils.WriteError("Failed to spoof Machine GUID");
        }
    }
}
