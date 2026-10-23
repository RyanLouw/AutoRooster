using System.Security.Cryptography;
using SchoolExamScheduler.Licensing;
using Xunit;
namespace SchoolExamScheduler.Licensing.Tests;
public sealed class LicencePolicyTests
{
    [Fact]
    public void SignedGrantCannotBeChangedOrUsedOnAnotherInstallation()
    {
        using var rsa=RSA.Create(2048);var now=DateTimeOffset.UtcNow;
        var grant=new LicenceGrant(Guid.NewGuid(),"School",Guid.NewGuid(),LicenceCrypto.KeyHash("secret-key"),now,now.AddDays(30),now.AddHours(24),"nonce");
        var signed=LicenceCrypto.Sign(grant,rsa);
        Assert.NotNull(LicenceCrypto.Verify(signed,rsa.ExportSubjectPublicKeyInfoPem()));
        Assert.Null(LicenceCrypto.Verify(signed with{Payload=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"))},rsa.ExportSubjectPublicKeyInfoPem()));
        using var other=RSA.Create(2048);Assert.Null(LicenceCrypto.Verify(signed,other.ExportSubjectPublicKeyInfoPem()));
        Assert.False(LicenceCrypto.AllowsOffline(grant,Guid.NewGuid(),grant.KeyHash,now,now));
        Assert.False(LicenceCrypto.AllowsOffline(grant,grant.InstallationId,grant.KeyHash,now.AddHours(24),now));
        Assert.False(LicenceCrypto.AllowsOffline(grant,grant.InstallationId,grant.KeyHash,now.AddHours(-1),now));
        Assert.True(LicenceCrypto.AllowsOffline(grant,grant.InstallationId,grant.KeyHash,now.AddHours(1),now));
    }
    [Fact]
    public void PaidThroughDateIncludesTheEntireSouthAfricanDay()
    {
        var end=LicenceDates.PaidUntil(new(2026,10,31));
        Assert.Equal(new DateTimeOffset(2026,10,31,22,0,0,TimeSpan.Zero),end);
    }
}
