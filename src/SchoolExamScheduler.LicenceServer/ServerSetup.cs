using System.Text.Json;
namespace SchoolExamScheduler.LicenceServer;
public static class ServerSetup
{
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);var file=Path.Combine(folder,"server-settings.json");
        if(File.Exists(file))throw new InvalidOperationException("Setup already exists. Edit the existing protected server configuration instead of overwriting it.");
        Console.Write("Administrator username [owner]: ");var user=Console.ReadLine();if(string.IsNullOrWhiteSpace(user))user="owner";
        Console.Write("Administrator password (at least 16 characters): ");var password=Password();
        Console.Write("Repeat password: ");if(password!=Password())throw new ArgumentException("Passwords do not match.");
        Console.Write("Bank-transfer instructions / contact details (can be edited later): ");var instructions=Console.ReadLine()??"Contact the supplier.";
        var hash=AdminPasswords.Hash(password);
        File.WriteAllText(file,JsonSerializer.Serialize(new{Licensing=new{AdminUsername=user,AdminPasswordHash=hash,PaymentInstructions=instructions}},new JsonSerializerOptions{WriteIndented=true}));
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        Console.WriteLine($"Configuration saved to {file}. Keep this file and the server data directory private. Start the server without --setup.");
    }
    private static string Password()
    {
        if(Console.IsInputRedirected)throw new InvalidOperationException("Run setup in an interactive terminal so the password is not echoed or stored in command history.");
        var text=new System.Text.StringBuilder();while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(text.Length>0)text.Length--;}else if(!char.IsControl(key.KeyChar))text.Append(key.KeyChar);}Console.WriteLine();return text.ToString();
    }
}
