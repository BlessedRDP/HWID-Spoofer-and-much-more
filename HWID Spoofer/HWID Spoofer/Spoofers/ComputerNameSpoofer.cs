using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace HWID_Spoofer.Spoofers;

/// <summary>
/// Spoofs the Windows computer/hostname to a random desktop-style name.
/// </summary>
public static class ComputerNameSpoofer
{
    private const string ActiveNamePath = @"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName";
    private const string PendingNamePath = @"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName";
    private const string TcpParamsPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetComputerNameExW(int NameType, string lpBuffer);

    // ComputerNamePhysicalDnsHostname = 5
    private const int ComputerNamePhysicalDnsHostname = 5;

    public static string? GetCurrentName()
    {
        return HwidUtils.RegRead(Registry.LocalMachine, ActiveNamePath, "ComputerName")
            ?? Environment.MachineName;
    }

    public static void Spoof()
    {
        HwidUtils.WriteHeader("Computer Name Spoofing");

        var oldName = GetCurrentName();
        var newName = HwidUtils.RandomComputerName();

        bool success = true;

        // Registry: Active computer name (read-only at runtime, but set for next boot)
        success &= HwidUtils.RegWrite(Registry.LocalMachine, PendingNamePath, "ComputerName", newName);

        // Registry: TCP/IP hostname
        success &= HwidUtils.RegWrite(Registry.LocalMachine, TcpParamsPath, "Hostname", newName);
        success &= HwidUtils.RegWrite(Registry.LocalMachine, TcpParamsPath, "NV Hostname", newName);

        // Win32 API to set DNS hostname
        try
        {
            SetComputerNameExW(ComputerNamePhysicalDnsHostname, newName);
        }
        catch { /* May fail, that's okay — registry change takes effect after reboot */ }

        if (success)
        {
            HwidUtils.WriteChange("Computer Name", oldName, newName);
            HwidUtils.WriteSuccess("Computer name spoofed (reboot required)");
        }
        else
        {
            HwidUtils.WriteError("Some computer name changes failed");
        }
    }
}
