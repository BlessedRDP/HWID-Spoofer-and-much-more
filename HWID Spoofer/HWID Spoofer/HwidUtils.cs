using Microsoft.Win32;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace HWID_Spoofer;

/// <summary>
/// Utility helpers for the HWID Spoofer: random ID generation, registry access, admin checks.
/// </summary>
public static class HwidUtils
{
    private static readonly Random Rng = new();

    // ─── Admin check ────────────────────────────────────────────────────

    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ─── Random generators ──────────────────────────────────────────────

    public static string RandomMac()
    {
        byte[] bytes = new byte[6];
        Rng.NextBytes(bytes);
        bytes[0] = (byte)(bytes[0] & 0xFE | 0x02); // locally administered, unicast
        return string.Join("", bytes.Select(b => b.ToString("X2")));
    }

    public static string RandomSerial(int length = 20)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[Rng.Next(chars.Length)]);
        return sb.ToString();
    }

    public static string RandomHex(int length = 16)
    {
        byte[] bytes = new byte[length / 2 + 1];
        Rng.NextBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "")[..length];
    }

    public static string RandomGuid() => Guid.NewGuid().ToString();

    public static string RandomComputerName()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder("DESKTOP-");
        for (int i = 0; i < 7; i++)
            sb.Append(chars[Rng.Next(chars.Length)]);
        return sb.ToString();
    }

    public static string RandomProductId()
    {
        // Format: XXXXX-XXXXX-XXXXX-XXXXX
        var parts = new string[4];
        for (int p = 0; p < 4; p++)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++)
                sb.Append(Rng.Next(10));
            parts[p] = sb.ToString();
        }
        return string.Join("-", parts);
    }

    public static string RandomBiosDate()
    {
        int year = Rng.Next(2020, 2026);
        int month = Rng.Next(1, 13);
        int day = Rng.Next(1, 29);
        return $"{month:D2}/{day:D2}/{year}";
    }

    // ─── Registry helpers ───────────────────────────────────────────────

    public static string? RegRead(RegistryKey root, string path, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(path, false);
            return key?.GetValue(valueName)?.ToString();
        }
        catch { return null; }
    }

    public static bool RegWrite(RegistryKey root, string path, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String)
    {
        try
        {
            using var key = root.OpenSubKey(path, true) ?? root.CreateSubKey(path, true);
            key.SetValue(valueName, value, kind);
            return true;
        }
        catch (Exception ex)
        {
            WriteError($"  Failed to write {path}\\{valueName}: {ex.Message}");
            return false;
        }
    }

    public static bool RegDelete(RegistryKey root, string path, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(path, true);
            if (key == null) return false;
            key.DeleteValue(valueName, false);
            return true;
        }
        catch { return false; }
    }

    // ─── Console helpers ────────────────────────────────────────────────

    public static void WriteBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ╔═══════════════════════════════════════════════╗
  ║           H W I D   S P O O F E R            ║
  ║         Hardware Identity Randomizer          ║
  ╚═══════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    public static void WriteHeader(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n ► {text}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('─', 50));
        Console.ResetColor();
    }

    public static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {text}");
        Console.ResetColor();
    }

    public static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {text}");
        Console.ResetColor();
    }

    public static void WriteInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"  {text}");
        Console.ResetColor();
    }

    public static void WriteChange(string label, string? oldVal, string? newVal)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {label}: ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(oldVal ?? "(empty)");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" → ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(newVal ?? "(empty)");
        Console.ResetColor();
    }

    // ─── Backup / Restore ───────────────────────────────────────────────

    public static string BackupFilePath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "hwid_backup.json");

    public static void SaveBackup(Dictionary<string, string?> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(BackupFilePath, json);
    }

    public static Dictionary<string, string?>? LoadBackup()
    {
        if (!File.Exists(BackupFilePath)) return null;
        var json = File.ReadAllText(BackupFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
    }
}
