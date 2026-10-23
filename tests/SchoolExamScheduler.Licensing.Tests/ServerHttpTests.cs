using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolExamScheduler.LicenceServer;
using SchoolExamScheduler.Licensing;
using Xunit;
namespace SchoolExamScheduler.Licensing.Tests;
public sealed class ServerHttpTests
{
    private sealed class ServerFactory:WebApplicationFactory<Program>
    {
        public readonly string Folder=Path.Combine(Path.GetTempPath(),"licence-http-"+Guid.NewGuid().ToString("N"));
        public readonly MutableClock Clock=new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Licensing:DataPath"]=Folder,["Licensing:AdminUsername"]="owner",["Licensing:AdminPasswordHash"]=AdminPasswords.Hash("test owner password 123"),["Licensing:PaymentInstructions"]="Test bank instructions"}));
            builder.ConfigureServices(services=>{services.RemoveAll<TimeProvider>();services.AddSingleton<TimeProvider>(Clock);});
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(Folder))Directory.Delete(Folder,true);}
    }
    private static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> Post(HttpClient client,string url,string html,params (string Key,string Value)[] fields)
    {
        var values=fields.ToDictionary(x=>x.Key,x=>x.Value);values["__RequestVerificationToken"]=Token(html);Assert.NotEmpty(values["__RequestVerificationToken"]);
        return await client.PostAsync(url,new FormUrlEncodedContent(values));
    }
    [Fact] public async Task Owner_can_create_disable_and_extend_through_csrf_protected_admin_pages()
    {
        using var factory=new ServerFactory();using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost"),AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Username","owner"},{"Password","test owner password 123"}}))).StatusCode);
        var login=await client.GetStringAsync("/Login");
        Assert.Equal(HttpStatusCode.Redirect,(await Post(client,"/Login",login,("Username","owner"),("Password","test owner password 123"))).StatusCode);
        var admin=await client.GetStringAsync("/Admin");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Admin?handler=Create",new FormUrlEncodedContent(new Dictionary<string,string>{{"School","Injected"}}))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(client,"/Admin?handler=Create",admin,("School","Example High"),("Contact","owner@example.test"),("PaidThrough","2026-10-31"),("MaximumDevices","2"))).StatusCode);
        admin=await client.GetStringAsync("/Admin");
        var key=WebUtility.HtmlDecode(Regex.Match(admin,"id=\"new-key\">([^<]+)").Groups[1].Value);Assert.StartsWith("AR-",key);
        var request=new LicenceCheckRequest(key,Guid.NewGuid(),Guid.NewGuid().ToString("N"));
        var reply=await (await client.PostAsJsonAsync("/api/licences/check",request)).Content.ReadFromJsonAsync<LicenceReply>();Assert.Equal(LicenceStatus.Active,reply!.Status);
        var repo=factory.Services.GetRequiredService<LicenceRepository>();var row=repo.List().Single();
        Assert.Equal(HttpStatusCode.Redirect,(await Post(client,"/Admin?handler=Switch",admin,("Id",row.Id.ToString()),("Revision",row.Revision.ToString()),("Enabled","false"))).StatusCode);
        reply=await (await client.PostAsJsonAsync("/api/licences/check",request)).Content.ReadFromJsonAsync<LicenceReply>();Assert.Equal(LicenceStatus.Disabled,reply!.Status);
        admin=await client.GetStringAsync("/Admin");row=repo.List().Single();
        Assert.DoesNotContain("id=\"new-key\"",admin);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(client,"/Admin?handler=Extend",admin,("Id",row.Id.ToString()),("Revision",row.Revision.ToString()),("PaidThrough","2026-11-30"),("BankReference","EFT-001"))).StatusCode);
        Assert.Equal(new DateOnly(2026,11,30),LicenceDates.PaidThrough(repo.List().Single().PaidUntilUtc));Assert.False(repo.List().Single().Enabled);
        using var insecure=factory.CreateClient(new(){BaseAddress=new("http://localhost"),AllowAutoRedirect=false});Assert.Equal(HttpStatusCode.BadRequest,(await insecure.GetAsync("/health")).StatusCode);
    }
}
