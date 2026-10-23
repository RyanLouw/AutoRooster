using System.Net.Http.Json;
using System.Text.Json;
namespace SchoolExamScheduler.Licensing;

public sealed record ActivationData(Guid InstallationId,string LicenceKey,SignedLicenceGrant? Grant,DateTimeOffset LastSeenUtc,string PaymentInstructions="",string PaymentReference="");
public interface IActivationStore
{
    Task<ActivationData> ReadAsync(CancellationToken token=default);
    Task SaveAsync(ActivationData data,CancellationToken token=default);
}
public sealed record LicenceClientOptions(string ServerUrl,string PublicKeyPem,bool AllowInsecureLoopback=false);
public sealed class LicenceClient(HttpClient http,LicenceClientOptions options,IActivationStore store,TimeProvider? time=null) : ILicenceService
{
    private readonly TimeProvider clock=time??TimeProvider.System;
    private readonly SemaphoreSlim access=new(1,1);
    public string ServerAddress=>options.ServerUrl;
    public Task<LicenceState> ActivateAsync(string key,CancellationToken cancellationToken=default)=>CheckCoreAsync(key.Trim(),cancellationToken);
    public Task<LicenceState> CheckAsync(CancellationToken cancellationToken=default)=>CheckCoreAsync(null,cancellationToken);
    public async Task RequireWriteAccessAsync(CancellationToken cancellationToken=default)
    {
        var state=await CheckAsync(cancellationToken);
        if(!state.CanWrite)throw new LicenceRequiredException(state.Message+" Open Subscription to activate or refresh your licence. Existing reports and exports remain available.");
    }
    private async Task<LicenceState> CheckCoreAsync(string? newKey,CancellationToken token)
    {
        await access.WaitAsync(token);
        try
        {
            if(!Uri.TryCreate(options.ServerUrl,UriKind.Absolute,out var uri) || !(uri.Scheme=="https" || (options.AllowInsecureLoopback && uri.Scheme=="http" && uri.IsLoopback)) || string.IsNullOrWhiteSpace(options.PublicKeyPem))
                return new(LicenceStatus.ConfigurationError,"The supplier's licensing server has not been configured. Existing reports remain available.");
            var data=await store.ReadAsync(token);
            if(newKey is not null)
            {
                if(newKey.Length is <10 or >200)return new(LicenceStatus.Invalid,"Enter the licence key supplied for your school.");
                data=data with{LicenceKey=newKey,Grant=null};await store.SaveAsync(data,token);
            }
            if(string.IsNullOrWhiteSpace(data.LicenceKey))return new(LicenceStatus.NotActivated,"Enter your school's licence key to create or change rosters.");
            var now=clock.GetUtcNow();var nonce=Guid.NewGuid().ToString("N");
            try
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
                using var response=await http.PostAsJsonAsync(new Uri(uri,"/api/licences/check"),new LicenceCheckRequest(data.LicenceKey,data.InstallationId,nonce),timeout.Token);
                response.EnsureSuccessStatusCode();
                var reply=await response.Content.ReadFromJsonAsync<LicenceReply>(timeout.Token)??throw new JsonException();
                if(reply.Status!=LicenceStatus.Active)
                {
                    if(reply.Status is not(LicenceStatus.Disabled or LicenceStatus.Expired or LicenceStatus.Invalid or LicenceStatus.DeviceLimit))throw new JsonException();
                    await store.SaveAsync(data with{Grant=null,LastSeenUtc=now,PaymentInstructions=reply.PaymentInstructions,PaymentReference=reply.PaymentReference},token);
                    return new(reply.Status,reply.Message,PaymentInstructions:reply.PaymentInstructions,PaymentReference:reply.PaymentReference);
                }
                var grant=reply.Grant is null?null:LicenceCrypto.Verify(reply.Grant,options.PublicKeyPem);
                now=clock.GetUtcNow();
                if(grant is null || grant.Nonce!=nonce || grant.InstallationId!=data.InstallationId || grant.KeyHash!=LicenceCrypto.KeyHash(data.LicenceKey) || grant.PaidUntilUtc<=now || Math.Abs((grant.IssuedAtUtc-now).TotalMinutes)>5 || grant.OfflineUntilUtc>grant.IssuedAtUtc.AddHours(24) || grant.OfflineUntilUtc>grant.PaidUntilUtc)
                {
                    await store.SaveAsync(data with{Grant=null},token);
                    return new(LicenceStatus.Invalid,"The licence response could not be verified. Check your computer's date/time and contact the supplier.");
                }
                await store.SaveAsync(data with{Grant=reply.Grant,LastSeenUtc=now,PaymentInstructions=reply.PaymentInstructions,PaymentReference=reply.PaymentReference},token);
                return new(LicenceStatus.Active,"Licence active.",grant.School,grant.PaidUntilUtc,grant.OfflineUntilUtc,reply.PaymentInstructions,reply.PaymentReference);
            }
            catch(Exception e) when(e is HttpRequestException || (e is OperationCanceledException && !token.IsCancellationRequested))
            {
                now=clock.GetUtcNow();var grant=data.Grant is null?null:LicenceCrypto.Verify(data.Grant,options.PublicKeyPem);
                if(grant is not null && LicenceCrypto.AllowsOffline(grant,data.InstallationId,LicenceCrypto.KeyHash(data.LicenceKey),now,data.LastSeenUtc))
                {
                    await store.SaveAsync(data with{LastSeenUtc=now>data.LastSeenUtc?now:data.LastSeenUtc},token);
                    return new(LicenceStatus.OfflineGrace,"Server unreachable: using the signed offline allowance (maximum 24 hours).",grant.School,grant.PaidUntilUtc,grant.OfflineUntilUtc,data.PaymentInstructions,data.PaymentReference);
                }
                return new(LicenceStatus.Unavailable,"The licence server is unreachable and no valid offline allowance remains. Connect to the internet and refresh.",PaymentInstructions:data.PaymentInstructions,PaymentReference:data.PaymentReference);
            }
            catch(JsonException)
            {
                await store.SaveAsync(data with{Grant=null},token);return new(LicenceStatus.Invalid,"The licensing server returned an invalid response. Contact the supplier.");
            }
        }
        finally{access.Release();}
    }
}
