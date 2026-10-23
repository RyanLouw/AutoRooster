using System.Security.Cryptography;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.LicenceServer;

public static class AdminPasswords
{
    public static string Hash(string password)
    {
        if(password.Length<16)throw new ArgumentException("Use at least 16 characters for the administrator password.");
        var salt=RandomNumberGenerator.GetBytes(32);var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,310000,HashAlgorithmName.SHA256,32);
        return $"pbkdf2-sha256$310000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password,string encoded)
    {
        try
        {
            var parts=encoded.Split('$');if(parts.Length!=4 || parts[0]!="pbkdf2-sha256" || parts[1]!="310000" || password.Length>1024)return false;
            var salt=Convert.FromBase64String(parts[2]);var expected=Convert.FromBase64String(parts[3]);
            if(salt.Length!=32 || expected.Length!=32)return false;
            return CryptographicOperations.FixedTimeEquals(expected,Rfc2898DeriveBytes.Pbkdf2(password,salt,310000,HashAlgorithmName.SHA256,32));
        }
        catch(FormatException){return false;}
    }
}
public sealed class ServerOptions
{
    public string DataPath {get;set;}="";
    public string AdminUsername {get;set;}="owner";
    public string AdminPasswordHash {get;set;}="";
    public string PaymentInstructions {get;set;}="Contact the supplier for bank-transfer details. Include your school payment reference.";
    public string KnownProxy {get;set;}="";
    public string DatabasePath=>Path.Combine(DataPath,"licences.db");
}
public sealed class LicenceSigner : IDisposable
{
    private readonly RSA key;
    public LicenceSigner(ServerOptions options)
    {
        Directory.CreateDirectory(options.DataPath);var path=Path.Combine(options.DataPath,"signing-key.pem");
        if(!File.Exists(path))
        {
            if(File.Exists(options.DatabasePath))throw new InvalidOperationException("Signing key missing for an existing database. Restore it from your server backup.");
            using var created=RSA.Create(3072);File.WriteAllText(path,created.ExportPkcs8PrivateKeyPem());
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        }
        key=RSA.Create();key.ImportFromPem(File.ReadAllText(path));
    }
    public string PublicKey {get{lock(key)return key.ExportSubjectPublicKeyInfoPem();}}
    public SignedLicenceGrant Sign(LicenceGrant grant){lock(key)return LicenceCrypto.Sign(grant,key);}
    public void Dispose()=>key.Dispose();
}
