using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using SchoolExamScheduler.Licensing;
using Xunit;
namespace SchoolExamScheduler.Licensing.Tests;
public sealed class MutableClock:TimeProvider
{
    public DateTimeOffset Now=new(2026,10,23,10,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>Now;
}
public sealed class LicenceClientTests
{
    private sealed class MemoryActivation:IActivationStore
    {
        public ActivationData Data=new(Guid.NewGuid(),"AR-test-school-key",null,DateTimeOffset.MinValue);
        public Task<ActivationData> ReadAsync(CancellationToken token=default)=>Task.FromResult(Data);
        public Task SaveAsync(ActivationData data,CancellationToken token=default){Data=data;return Task.CompletedTask;}
    }
    private sealed class Handler(Func<LicenceCheckRequest,LicenceReply> reply):HttpMessageHandler
    {
        public bool Offline;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(Offline)throw new HttpRequestException("Offline");
            var data=(await request.Content!.ReadFromJsonAsync<LicenceCheckRequest>(cancellationToken))!;
            return new(HttpStatusCode.OK){Content=JsonContent.Create(reply(data))};
        }
    }
    [Fact] public async Task Active_then_offline_is_allowed_only_until_24_hours()
    {
        using var rsa=RSA.Create(2048);var clock=new MutableClock();var cache=new MemoryActivation();
        using var handler=new Handler(r=>new(LicenceStatus.Active,"ok","Bank details","REF",LicenceCrypto.Sign(new(Guid.NewGuid(),"School",r.InstallationId,LicenceCrypto.KeyHash(r.LicenceKey),clock.Now,clock.Now.AddDays(30),clock.Now.AddHours(24),r.Nonce),rsa)));
        using var http=new HttpClient(handler);var client=new LicenceClient(http,new("https://licences.test",rsa.ExportRSAPublicKeyPem()),cache,clock);
        Assert.Equal(LicenceStatus.Active,(await client.CheckAsync()).Status);
        handler.Offline=true;clock.Now=clock.Now.AddHours(23);
        var offline=await client.CheckAsync();Assert.True(offline.CanWrite);Assert.Equal("REF",offline.PaymentReference);
        clock.Now=clock.Now.AddHours(1);Assert.False((await client.CheckAsync()).CanWrite);
        await Assert.ThrowsAsync<LicenceRequiredException>(()=>client.RequireWriteAccessAsync());
    }
    [Theory] [InlineData(LicenceStatus.Disabled)] [InlineData(LicenceStatus.Expired)] [InlineData(LicenceStatus.DeviceLimit)] [InlineData(LicenceStatus.Invalid)]
    public async Task Online_denial_erases_cached_allowance(LicenceStatus status)
    {
        using var rsa=RSA.Create(2048);var clock=new MutableClock();var cache=new MemoryActivation();LicenceStatus response=LicenceStatus.Active;
        using var handler=new Handler(r=>new(response,"denied","Pay supplier","REF",response==LicenceStatus.Active?LicenceCrypto.Sign(new(Guid.NewGuid(),"School",r.InstallationId,LicenceCrypto.KeyHash(r.LicenceKey),clock.Now,clock.Now.AddDays(30),clock.Now.AddHours(24),r.Nonce),rsa):null));
        using var http=new HttpClient(handler);var client=new LicenceClient(http,new("https://licences.test",rsa.ExportRSAPublicKeyPem()),cache,clock);
        Assert.True((await client.CheckAsync()).CanWrite);response=status;
        Assert.Equal(status,(await client.CheckAsync()).Status);Assert.Null(cache.Data.Grant);
        handler.Offline=true;Assert.False((await client.CheckAsync()).CanWrite);
    }
    [Fact] public async Task Expiry_caps_offline_time_and_clock_rollback_is_rejected()
    {
        using var rsa=RSA.Create(2048);var clock=new MutableClock();var cache=new MemoryActivation();
        using var handler=new Handler(r=>new(LicenceStatus.Active,"ok","","",LicenceCrypto.Sign(new(Guid.NewGuid(),"School",r.InstallationId,LicenceCrypto.KeyHash(r.LicenceKey),clock.Now,clock.Now.AddHours(2),clock.Now.AddHours(2),r.Nonce),rsa)));
        using var http=new HttpClient(handler);var client=new LicenceClient(http,new("https://licences.test",rsa.ExportRSAPublicKeyPem()),cache,clock);
        Assert.True((await client.CheckAsync()).CanWrite);handler.Offline=true;
        clock.Now=clock.Now.AddHours(-1);Assert.False((await client.CheckAsync()).CanWrite);
        clock.Now=clock.Now.AddHours(3);Assert.False((await client.CheckAsync()).CanWrite);
    }
    [Fact] public async Task Forged_grant_and_http_production_configuration_are_rejected()
    {
        using var rsa=RSA.Create(2048);using var attacker=RSA.Create(2048);var clock=new MutableClock();var cache=new MemoryActivation();
        using var handler=new Handler(r=>new(LicenceStatus.Active,"ok","","",LicenceCrypto.Sign(new(Guid.NewGuid(),"School",r.InstallationId,LicenceCrypto.KeyHash(r.LicenceKey),clock.Now,clock.Now.AddDays(30),clock.Now.AddHours(24),r.Nonce),attacker)));
        using var http=new HttpClient(handler);
        Assert.Equal(LicenceStatus.Invalid,(await new LicenceClient(http,new("https://licences.test",rsa.ExportRSAPublicKeyPem()),cache,clock).CheckAsync()).Status);
        Assert.Equal(LicenceStatus.ConfigurationError,(await new LicenceClient(http,new("http://licences.test",rsa.ExportRSAPublicKeyPem(),true),cache,clock).CheckAsync()).Status);
        Assert.Equal(LicenceStatus.ConfigurationError,(await new LicenceClient(http,new("http://localhost:5080",rsa.ExportRSAPublicKeyPem()),cache,clock).CheckAsync()).Status);
    }
}
