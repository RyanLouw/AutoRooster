using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.LicenceServer;

public sealed record SchoolLicence(Guid Id,string School,string Contact,string PaymentReference,string KeySuffix,DateTimeOffset PaidUntilUtc,bool Enabled,long Revision,int MaximumDevices,int Devices);
public sealed record LicenceAudit(string Action,string Actor,string Note,DateTimeOffset AtUtc);
public sealed class LicenceRepository(ServerOptions options,LicenceSigner signer,TimeProvider clock)
{
    private SqliteConnection Open()
    {
        var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=options.DatabasePath,ForeignKeys=true,DefaultTimeout=10}.ConnectionString);db.Open();return db;
    }
    private static SqliteCommand Command(SqliteConnection db,SqliteTransaction? transaction,string sql,params (string Name,object Value)[] args)
    {
        var command=db.CreateCommand();command.Transaction=transaction;command.CommandText=sql;
        foreach(var (name,value) in args)command.Parameters.AddWithValue(name,value);return command;
    }
    public void Initialize()
    {
        using var db=Open();using var version=Command(db,null,"PRAGMA user_version");var current=Convert.ToInt32(version.ExecuteScalar());
        if(current>1)throw new InvalidOperationException("The licence database needs a newer server version.");
        using var command=Command(db,null,"""
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Licences(Id TEXT PRIMARY KEY,School TEXT NOT NULL,Contact TEXT NOT NULL,PaymentReference TEXT NOT NULL UNIQUE,KeyHash TEXT NOT NULL UNIQUE,KeySuffix TEXT NOT NULL,PaidUntil INTEGER NOT NULL,Enabled INTEGER NOT NULL,Revision INTEGER NOT NULL,MaximumDevices INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS Installations(LicenceId TEXT NOT NULL REFERENCES Licences(Id),InstallationId TEXT NOT NULL,LastSeen INTEGER NOT NULL,PRIMARY KEY(LicenceId,InstallationId));
            CREATE TABLE IF NOT EXISTS Audit(Id INTEGER PRIMARY KEY AUTOINCREMENT,LicenceId TEXT NOT NULL REFERENCES Licences(Id),Action TEXT NOT NULL,Actor TEXT NOT NULL,Note TEXT NOT NULL,AtUtc INTEGER NOT NULL);
            PRAGMA user_version=1;
            """);command.ExecuteNonQuery();
    }
    private const string Select="SELECT Id,School,Contact,PaymentReference,KeySuffix,PaidUntil,Enabled,Revision,MaximumDevices,(SELECT COUNT(*) FROM Installations i WHERE i.LicenceId=l.Id) FROM Licences l ";
    private static SchoolLicence Read(SqliteDataReader r)=>new(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),new(r.GetInt64(5),TimeSpan.Zero),r.GetBoolean(6),r.GetInt64(7),r.GetInt32(8),r.GetInt32(9));
    public IReadOnlyList<SchoolLicence> List()
    {
        using var db=Open();using var command=Command(db,null,Select+"ORDER BY School,Id");using var reader=command.ExecuteReader();var rows=new List<SchoolLicence>();while(reader.Read())rows.Add(Read(reader));return rows;
    }
    public IReadOnlyList<LicenceAudit> Audit(Guid id)
    {
        using var db=Open();using var command=Command(db,null,"SELECT Action,Actor,Note,AtUtc FROM Audit WHERE LicenceId=$id ORDER BY Id DESC LIMIT 20",("$id",id.ToString("N")));
        using var reader=command.ExecuteReader();var rows=new List<LicenceAudit>();while(reader.Read())rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),new(reader.GetInt64(3),TimeSpan.Zero)));return rows;
    }
    private static string Key()=>"AR-"+Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string Text(string? text,int maximum,bool required=false)
    {text=text?.Trim()??"";if(text.Length>maximum || (required&&text.Length==0))throw new ArgumentException($"Enter {(required?"a value":"text")} of at most {maximum} characters.");return text;}
    private DateTimeOffset End(DateOnly date)
    {
        var today=DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(2)).DateTime);
        if(date<today.AddYears(-1) || date>today.AddYears(5))throw new ArgumentException("Choose a paid-through date within the supported range (last year to five years ahead).");
        return LicenceDates.PaidUntil(date).ToUniversalTime();
    }
    private void Log(SqliteConnection db,SqliteTransaction transaction,Guid id,string action,string actor,string note)
    {
        using var command=Command(db,transaction,"INSERT INTO Audit(LicenceId,Action,Actor,Note,AtUtc) VALUES($id,$action,$actor,$note,$at)",("$id",id.ToString("N")),("$action",action),("$actor",actor),("$note",note),("$at",clock.GetUtcNow().UtcTicks));command.ExecuteNonQuery();
    }
    public (Guid Id,string Key) Create(string school,string? contact,DateOnly paidThrough,int maximumDevices,string actor)
    {
        school=Text(school,200,true);contact=Text(contact,254);if(maximumDevices is <1 or >20)throw new ArgumentException("Allow between 1 and 20 installations.");
        var until=End(paidThrough);var key=Key();var id=Guid.NewGuid();using var db=Open();using var transaction=db.BeginTransaction();
        using var command=Command(db,transaction,"INSERT INTO Licences VALUES($id,$school,$contact,$reference,$hash,$suffix,$until,1,1,$maximum)",("$id",id.ToString("N")),("$school",school),("$contact",contact),("$reference","AR-"+id.ToString("N")[..10].ToUpperInvariant()),("$hash",LicenceCrypto.KeyHash(key)),("$suffix",key[^6..]),("$until",until.UtcTicks),("$maximum",maximumDevices));command.ExecuteNonQuery();Log(db,transaction,id,"Created",actor,$"Paid through {paidThrough:yyyy-MM-dd}; maximum installations {maximumDevices}.");transaction.Commit();return(id,key);
    }
    private void Change(Guid id,long revision,string action,string actor,string note,Action<SqliteConnection,SqliteTransaction,SchoolLicence> update)
    {
        using var db=Open();using var transaction=db.BeginTransaction();SchoolLicence current;
        using(var command=Command(db,transaction,Select+"WHERE l.Id=$id",("$id",id.ToString("N"))))
        using(var reader=command.ExecuteReader()){if(!reader.Read())throw new ArgumentException("Licence not found.");current=Read(reader);}
        if(current.Revision!=revision)throw new InvalidOperationException("This licence changed in another session. Reload before trying again.");
        update(db,transaction,current);
        using(var command=Command(db,transaction,"UPDATE Licences SET Revision=Revision+1 WHERE Id=$id",("$id",id.ToString("N"))))command.ExecuteNonQuery();
        Log(db,transaction,id,action,actor,Text(note,1000));transaction.Commit();
    }
    public void Extend(Guid id,long revision,DateOnly paidThrough,string paymentReference,string? note,string actor)
    {
        paymentReference=Text(paymentReference,120,true);var until=End(paidThrough);
        Change(id,revision,"Payment / extended",actor,$"Bank reference: {paymentReference}; paid through {paidThrough:yyyy-MM-dd}. {note}",(db,transaction,current)=>
        {
            if(until<=current.PaidUntilUtc)throw new ArgumentException("Choose a paid-through date later than the current expiry. Use Disable to stop access immediately.");
            using var command=Command(db,transaction,"UPDATE Licences SET PaidUntil=$until WHERE Id=$id",("$until",until.UtcTicks),("$id",id.ToString("N")));command.ExecuteNonQuery();
        });
    }
    public void SetEnabled(Guid id,long revision,bool enabled,string actor)=>Change(id,revision,enabled?"Enabled":"Disabled",actor,"Manual access switch.",(db,transaction,_)=>
    {using var command=Command(db,transaction,"UPDATE Licences SET Enabled=$enabled WHERE Id=$id",("$enabled",enabled?1:0),("$id",id.ToString("N")));command.ExecuteNonQuery();});
    public string RotateKey(Guid id,long revision,string actor)
    {
        var key=Key();Change(id,revision,"Key replaced",actor,"Old key no longer accepted online.",(db,transaction,_)=>
        {using var command=Command(db,transaction,"UPDATE Licences SET KeyHash=$hash,KeySuffix=$suffix WHERE Id=$id",("$hash",LicenceCrypto.KeyHash(key)),("$suffix",key[^6..]),("$id",id.ToString("N")));command.ExecuteNonQuery();});return key;
    }
    public void ResetInstallations(Guid id,long revision,string actor)=>Change(id,revision,"Installations reset",actor,"Activation slots released. Replace the key too if an old computer should lose access.",(db,transaction,_)=>
    {using var command=Command(db,transaction,"DELETE FROM Installations WHERE LicenceId=$id",("$id",id.ToString("N")));command.ExecuteNonQuery();});
    public LicenceReply Check(LicenceCheckRequest request)
    {
        LicenceReply Reply(LicenceStatus state,string message,string reference="")=>new(state,message,options.PaymentInstructions,reference);
        if(string.IsNullOrWhiteSpace(request.LicenceKey) || request.LicenceKey.Length>200 || request.InstallationId==Guid.Empty || request.Nonce?.Length!=32 || !Guid.TryParseExact(request.Nonce,"N",out _))return Reply(LicenceStatus.Invalid,"Invalid licence or installation details.");
        using var db=Open();using var transaction=db.BeginTransaction();SchoolLicence licence;
        using(var command=Command(db,transaction,Select+"WHERE KeyHash=$hash",("$hash",LicenceCrypto.KeyHash(request.LicenceKey))))
        using(var reader=command.ExecuteReader()){if(!reader.Read())return Reply(LicenceStatus.Invalid,"Licence key not recognised. Contact the supplier.");licence=Read(reader);}
        var now=clock.GetUtcNow();
        if(!licence.Enabled)return Reply(LicenceStatus.Disabled,"This school licence has been switched off. Contact the supplier.",licence.PaymentReference);
        if(now>=licence.PaidUntilUtc)return Reply(LicenceStatus.Expired,$"Subscription expired (paid through {LicenceDates.PaidThrough(licence.PaidUntilUtc):dd MMM yyyy}). Contact the supplier after making your bank payment.",licence.PaymentReference);
        long exists;using(var query=Command(db,transaction,"SELECT COUNT(*) FROM Installations WHERE LicenceId=$id AND InstallationId=$installation",("$id",licence.Id.ToString("N")),("$installation",request.InstallationId.ToString("N"))))exists=(long)query.ExecuteScalar()!;
        if(exists==0 && licence.Devices>=licence.MaximumDevices)return Reply(LicenceStatus.DeviceLimit,"This licence has reached its installation limit. Contact the supplier.",licence.PaymentReference);
        using(var command=Command(db,transaction,"INSERT INTO Installations VALUES($id,$installation,$now) ON CONFLICT(LicenceId,InstallationId) DO UPDATE SET LastSeen=excluded.LastSeen",("$id",licence.Id.ToString("N")),("$installation",request.InstallationId.ToString("N")),("$now",now.UtcTicks)))command.ExecuteNonQuery();
        transaction.Commit();var offline=now.AddHours(24);if(offline>licence.PaidUntilUtc)offline=licence.PaidUntilUtc;
        return new(LicenceStatus.Active,"Licence active.",options.PaymentInstructions,licence.PaymentReference,signer.Sign(new(licence.Id,licence.School,request.InstallationId,LicenceCrypto.KeyHash(request.LicenceKey),now,licence.PaidUntilUtc,offline,request.Nonce)));
    }
}
