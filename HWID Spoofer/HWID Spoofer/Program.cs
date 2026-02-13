using HWID_Spoofer;
using HWID_Spoofer.Spoofers;

// ── Startup ─────────────────────────────────────────────────────────────
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "HWID Spoofer";

HwidUtils.WriteBanner();

// Greeting with system snapshot
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Welcome, {Environment.UserName}");
Console.WriteLine($"  Machine: {Environment.MachineName}  |  OS: {Environment.OSVersion.Version}  |  {DateTime.Now:yyyy-MM-dd HH:mm}");
Console.ResetColor();

if (!HwidUtils.IsRunningAsAdmin())
{
    HwidUtils.WriteError("This application must be run as Administrator!");
    HwidUtils.WriteInfo("Right-click → Run as administrator, or the manifest should auto-elevate.");
    Console.ReadKey();
    return;
}

HwidUtils.WriteSuccess("Running with Administrator privileges");

// ── Main Menu Loop ──────────────────────────────────────────────────────
while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"
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
  └─────────────────────────────────────────────┘");
    Console.ResetColor();

    Console.Write("\n  Select option: ");
    var input = Console.ReadLine()?.Trim();

    switch (input)
    {
        case "1":
            ShowCurrentIds();
            break;
        case "2":
            SpoofAll();
            break;
        case "3":
            SpoofIndividual();
            break;
        case "4":
            BackupIds();
            break;
        case "5":
            RestoreIds();
            break;
        case "6":
            SpoofVerifier.RunFullVerification();
            break;
        case "7":
            DebugDiagnostics.RunFullDiagnostics();
            break;
        case "0":
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  Reboot your PC for all changes to take full effect.");
            Console.ResetColor();
            return;
        default:
            HwidUtils.WriteError("Invalid option");
            break;
    }
}

// ── Show Current IDs ────────────────────────────────────────────────────
void ShowCurrentIds()
{
    HwidUtils.WriteHeader("Current Hardware Identifiers");

    // Machine GUID
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"  Machine GUID:     {MachineGuidSpoofer.GetCurrentGuid() ?? "(not found)"}");

    // Computer Name
    Console.WriteLine($"  Computer Name:    {ComputerNameSpoofer.GetCurrentName() ?? "(not found)"}");

    // BIOS / SMBIOS
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine("\n  ─── BIOS / SMBIOS ───");
    Console.ForegroundColor = ConsoleColor.White;
    foreach (var kv in BiosSpoofer.GetCurrentIds())
        Console.WriteLine($"  {kv.Key,-25} {kv.Value}");

    // MAC Addresses
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine("\n  ─── MAC Addresses ───");
    Console.ForegroundColor = ConsoleColor.White;
    foreach (var kv in MacAddressSpoofer.GetCurrentMacs())
    {
        var parts = kv.Key.Split('|');
        var label = parts.Length > 1 ? parts[1] : parts[0];
        Console.WriteLine($"  {label,-35} {kv.Value}");
    }

    // Disk IDs
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine("\n  ─── Disk Identifiers ───");
    Console.ForegroundColor = ConsoleColor.White;
    foreach (var kv in DiskIdSpoofer.GetCurrentIds())
    {
        var shortKey = kv.Key;
        if (shortKey.Length > 60)
            shortKey = "..." + shortKey[^57..];
        Console.WriteLine($"  {shortKey,-60} {kv.Value}");
    }

    // Product IDs
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine("\n  ─── Windows Product IDs ───");
    Console.ForegroundColor = ConsoleColor.White;
    foreach (var kv in ProductIdSpoofer.GetCurrentIds())
        Console.WriteLine($"  {kv.Key,-25} {kv.Value}");

    Console.ResetColor();
}

// ── Spoof All ───────────────────────────────────────────────────────────
void SpoofAll()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("\n  ⚠ This will change ALL hardware identifiers. Continue? (y/n): ");
    Console.ResetColor();

    if (Console.ReadLine()?.Trim().ToLower() != "y")
    {
        HwidUtils.WriteInfo("Cancelled.");
        return;
    }

    MachineGuidSpoofer.Spoof();
    ComputerNameSpoofer.Spoof();
    BiosSpoofer.Spoof();
    MacAddressSpoofer.Spoof();
    DiskIdSpoofer.Spoof();
    ProductIdSpoofer.Spoof();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(@"
  ╔═══════════════════════════════════════════════╗
  ║        ALL IDENTIFIERS SPOOFED!               ║
  ║  Please REBOOT your PC for full effect.       ║
  ╚═══════════════════════════════════════════════╝");
    Console.ResetColor();
}

// ── Spoof Individual ────────────────────────────────────────────────────
void SpoofIndividual()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"
  ┌─────────────────────────────────────────────┐
  │  1.  MAC Address                            │
  │  2.  Disk Serial Numbers                    │
  │  3.  Machine GUID                           │
  │  4.  BIOS / SMBIOS                          │
  │  5.  Computer Name                          │
  │  6.  Windows Product ID                     │
  │  0.  Back                                   │
  └─────────────────────────────────────────────┘");
    Console.ResetColor();

    Console.Write("\n  Select: ");
    var input = Console.ReadLine()?.Trim();

    switch (input)
    {
        case "1": MacAddressSpoofer.Spoof(); break;
        case "2": DiskIdSpoofer.Spoof(); break;
        case "3": MachineGuidSpoofer.Spoof(); break;
        case "4": BiosSpoofer.Spoof(); break;
        case "5": ComputerNameSpoofer.Spoof(); break;
        case "6": ProductIdSpoofer.Spoof(); break;
        case "0": return;
        default: HwidUtils.WriteError("Invalid option"); break;
    }
}

// ── Backup ──────────────────────────────────────────────────────────────
void BackupIds()
{
    HwidUtils.WriteHeader("Backing Up Current IDs");

    var backup = new Dictionary<string, string?>();

    // Machine GUID
    backup["MachineGuid"] = MachineGuidSpoofer.GetCurrentGuid();

    // Computer Name
    backup["ComputerName"] = ComputerNameSpoofer.GetCurrentName();

    // BIOS
    foreach (var kv in BiosSpoofer.GetCurrentIds())
        backup[$"BIOS_{kv.Key}"] = kv.Value;

    // MAC
    foreach (var kv in MacAddressSpoofer.GetCurrentMacs())
        backup[$"MAC_{kv.Key}"] = kv.Value;

    // Disk
    foreach (var kv in DiskIdSpoofer.GetCurrentIds())
        backup[$"Disk_{kv.Key}"] = kv.Value;

    // Product
    foreach (var kv in ProductIdSpoofer.GetCurrentIds())
        backup[$"Product_{kv.Key}"] = kv.Value;

    HwidUtils.SaveBackup(backup);
    HwidUtils.WriteSuccess($"Backup saved to: {HwidUtils.BackupFilePath}");
    HwidUtils.WriteInfo($"Total entries: {backup.Count}");
}

// ── Restore ─────────────────────────────────────────────────────────────
void RestoreIds()
{
    HwidUtils.WriteHeader("Restoring IDs from Backup");

    var backup = HwidUtils.LoadBackup();
    if (backup == null)
    {
        HwidUtils.WriteError($"No backup file found at: {HwidUtils.BackupFilePath}");
        HwidUtils.WriteInfo("Run 'Backup Current IDs' first before spoofing.");
        return;
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"\n  ⚠ Restore {backup.Count} values from backup? (y/n): ");
    Console.ResetColor();

    if (Console.ReadLine()?.Trim().ToLower() != "y")
    {
        HwidUtils.WriteInfo("Cancelled.");
        return;
    }

    int restored = 0;

    // Machine GUID
    if (backup.TryGetValue("MachineGuid", out var mguid) && mguid != null)
    {
        if (HwidUtils.RegWrite(Microsoft.Win32.Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Cryptography", "MachineGuid", mguid))
        {
            HwidUtils.WriteSuccess($"MachineGuid → {mguid}");
            restored++;
        }
    }

    // Computer Name
    if (backup.TryGetValue("ComputerName", out var cname) && cname != null)
    {
        if (HwidUtils.RegWrite(Microsoft.Win32.Registry.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName", "ComputerName", cname))
        {
            HwidUtils.WriteSuccess($"ComputerName → {cname}");
            restored++;
        }
    }

    HwidUtils.WriteSuccess($"Restored {restored} identifier(s). Reboot for full effect.");
}
