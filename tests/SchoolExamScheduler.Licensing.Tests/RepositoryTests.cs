using SchoolExamScheduler.LicenceServer;
using SchoolExamScheduler.Licensing;
using Xunit;
namespace SchoolExamScheduler.Licensing.Tests;
public sealed class RepositoryTests:IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"licence-tests-"+Guid.NewGuid().ToString("N"));
    private readonly MutableClock clock=new();private readonly LicenceSigner signer;private readonly LicenceRepository repo;
    public RepositoryTests(){var options=new ServerOptions{DataPath=folder};signer=new(options);repo=new(options,signer,clock);repo.Initialize();}
    public void Dispose(){signer.Dispose();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
    private LicenceReply Check(string key,Guid? installation=null)=>repo.Check(new(key,installation??Guid.NewGuid(),Guid.NewGuid().ToString("N")));
    [Fact] public void Payment_extension_and_manual_switch_are_independent_and_audited()
    {
        var created=repo.Create("School","contact",new(2026,10,23),2,"owner");var first=repo.List().Single();
        var reply=Check(created.Key);Assert.Equal(LicenceStatus.Active,reply.Status);
        var grant=LicenceCrypto.Verify(reply.Grant!,signer.PublicKey)!;Assert.Equal(first.PaidUntilUtc,grant.OfflineUntilUtc);
        clock.Now=new(2026,10,23,22,0,0,TimeSpan.Zero);Assert.Equal(LicenceStatus.Expired,Check(created.Key).Status);
        repo.SetEnabled(created.Id,first.Revision,false,"owner");
        var disabled=repo.List().Single();repo.Extend(created.Id,disabled.Revision,new(2026,11,30),"BANK-001","October payment","owner");
        Assert.Equal(LicenceStatus.Disabled,Check(created.Key).Status);
        repo.SetEnabled(created.Id,repo.List().Single().Revision,true,"owner");Assert.Equal(LicenceStatus.Active,Check(created.Key).Status);
        Assert.Contains(repo.Audit(created.Id),a=>a.Action=="Payment / extended"&&a.Note.Contains("BANK-001"));
        Assert.Throws<InvalidOperationException>(()=>repo.SetEnabled(created.Id,first.Revision,false,"owner"));
    }
    [Fact] public void Installation_limit_key_replacement_and_restart_preserve_security()
    {
        var created=repo.Create("School","",new(2026,12,31),1,"owner");var installation=Guid.NewGuid();
        Assert.Equal(LicenceStatus.Active,Check(created.Key,installation).Status);
        Assert.Equal(LicenceStatus.Active,Check(created.Key,installation).Status);
        Assert.Equal(LicenceStatus.DeviceLimit,Check(created.Key).Status);
        repo.ResetInstallations(created.Id,repo.List().Single().Revision,"owner");Assert.Equal(LicenceStatus.Active,Check(created.Key).Status);
        var replacement=repo.RotateKey(created.Id,repo.List().Single().Revision,"owner");Assert.Equal(LicenceStatus.Invalid,Check(created.Key).Status);
        using var persisted=new LicenceSigner(new ServerOptions{DataPath=folder});Assert.Equal(signer.PublicKey,persisted.PublicKey);
        using var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(folder,"licences.db"));db.Open();
        using var command=db.CreateCommand();command.CommandText="SELECT KeyHash FROM Licences";Assert.Equal(LicenceCrypto.KeyHash(replacement),command.ExecuteScalar());
        Assert.DoesNotContain(created.Key,string.Join(" ",repo.Audit(created.Id).Select(a=>a.Note)));
    }
    [Fact] public void Passwords_are_salted_and_wrong_or_malformed_credentials_fail()
    {
        const string password="a long test password";var hash=AdminPasswords.Hash(password);
        Assert.NotEqual(hash,AdminPasswords.Hash(password));Assert.True(AdminPasswords.Verify(password,hash));
        Assert.False(AdminPasswords.Verify("wrong",hash));Assert.False(AdminPasswords.Verify(password,"broken"));
    }
}
