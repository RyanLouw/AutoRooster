using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace SchoolExamScheduler.Licensing;

public enum LicenceStatus { NotActivated,Active,OfflineGrace,Expired,Disabled,Invalid,Unavailable,DeviceLimit,ConfigurationError }
public sealed record LicenceCheckRequest(string LicenceKey,Guid InstallationId,string Nonce);
public sealed record LicenceGrant(Guid LicenceId,string School,Guid InstallationId,string KeyHash,DateTimeOffset IssuedAtUtc,DateTimeOffset PaidUntilUtc,DateTimeOffset OfflineUntilUtc,string Nonce);
public sealed record SignedLicenceGrant(string Payload,string Signature);
public sealed record LicenceReply(LicenceStatus Status,string Message,string PaymentInstructions,string PaymentReference,SignedLicenceGrant? Grant=null);
public sealed record LicenceState(LicenceStatus Status,string Message,string School="",DateTimeOffset? PaidUntilUtc=null,DateTimeOffset? OfflineUntilUtc=null,string PaymentInstructions="",string PaymentReference="")
{
    public bool CanWrite=>Status is LicenceStatus.Active or LicenceStatus.OfflineGrace;
}
public interface ILicenceGate { Task RequireWriteAccessAsync(CancellationToken cancellationToken=default); }
public interface ILicenceService : ILicenceGate
{
    string ServerAddress {get;}
    Task<LicenceState> CheckAsync(CancellationToken cancellationToken=default);
    Task<LicenceState> ActivateAsync(string key,CancellationToken cancellationToken=default);
}
public sealed class LicenceRequiredException(string message) : InvalidOperationException(message);
public static class LicenceDates
{
    public static DateTimeOffset PaidUntil(DateOnly inclusiveDate)=>new(inclusiveDate.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(2));
    public static DateOnly PaidThrough(DateTimeOffset exclusiveEnd)=>DateOnly.FromDateTime(exclusiveEnd.ToOffset(TimeSpan.FromHours(2)).AddTicks(-1).DateTime);
}
public static class LicenceCrypto
{
    public static string KeyHash(string key)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));
    public static SignedLicenceGrant Sign(LicenceGrant grant,RSA key)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(grant);
        return new(Convert.ToBase64String(bytes),Convert.ToBase64String(key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)));
    }
    public static LicenceGrant? Verify(SignedLicenceGrant signed,string publicKey)
    {
        try
        {
            if(string.IsNullOrEmpty(signed.Payload) || string.IsNullOrEmpty(signed.Signature))return null;
            if(signed.Payload.Length>16384 || signed.Signature.Length>4096)return null;
            var bytes=Convert.FromBase64String(signed.Payload);using var key=RSA.Create();key.ImportFromPem(publicKey);
            return key.VerifyData(bytes,Convert.FromBase64String(signed.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)?JsonSerializer.Deserialize<LicenceGrant>(bytes):null;
        }
        catch(Exception e) when(e is CryptographicException or FormatException or ArgumentException or JsonException){return null;}
    }
    public static bool AllowsOffline(LicenceGrant grant,Guid installation,string keyHash,DateTimeOffset now,DateTimeOffset lastSeen)=>
        grant.InstallationId==installation && grant.KeyHash==keyHash && grant.IssuedAtUtc<=now.AddMinutes(5) && now>=lastSeen.AddMinutes(-5) &&
        grant.OfflineUntilUtc<=grant.IssuedAtUtc.AddHours(24) && now<grant.OfflineUntilUtc && now<grant.PaidUntilUtc;
}
