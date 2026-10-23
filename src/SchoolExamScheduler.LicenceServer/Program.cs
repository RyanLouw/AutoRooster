using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using SchoolExamScheduler.Licensing;
using SchoolExamScheduler.LicenceServer;

var dataPath=Environment.GetEnvironmentVariable("Licensing__DataPath")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AutoRoosterLicenceServer");
if(args.Contains("--setup")){ServerSetup.Run(dataPath);return;}
var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(Path.Combine(dataPath,"server-settings.json"),optional:true).AddEnvironmentVariables();
builder.Services.AddSingleton(sp=>
{
    var options=new ServerOptions{DataPath=dataPath};sp.GetRequiredService<IConfiguration>().GetSection("Licensing").Bind(options);
    options.DataPath=Path.GetFullPath(options.DataPath);
    if(options.AdminPasswordHash.Split('$') is not ["pbkdf2-sha256","310000",_,_] || string.IsNullOrWhiteSpace(options.AdminUsername))throw new InvalidOperationException("Administrator credentials are missing. Run the server with --setup, or configure Licensing__AdminPasswordHash before starting.");
    return options;
});
builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddSingleton<LicenceSigner>();builder.Services.AddSingleton<LicenceRepository>();
builder.Services.AddDataProtection().SetApplicationName("AutoRooster.LicenceServer");
builder.Services.AddOptions<KeyManagementOptions>().Configure<ServerOptions,ILoggerFactory>((o,settings,logs)=>o.XmlRepository=new FileSystemXmlRepository(new DirectoryInfo(Path.Combine(settings.DataPath,"cookie-keys")),logs));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>
{
    o.LoginPath="/Login";o.Cookie.Name="AutoRooster.Admin";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
});
builder.Services.AddAuthorization();builder.Services.AddRazorPages(o=>o.Conventions.AuthorizeFolder("/Admin"));
builder.Services.AddRateLimiter(o=>
{
    o.RejectionStatusCode=429;
    o.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(5),QueueLimit=0}));
    o.AddPolicy("checks",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=16384);
var app=builder.Build();var settings=app.Services.GetRequiredService<ServerOptions>();
_ = app.Services.GetRequiredService<LicenceSigner>();app.Services.GetRequiredService<LicenceRepository>().Initialize();
var forwarded=new ForwardedHeadersOptions{ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto};
if(!string.IsNullOrWhiteSpace(settings.KnownProxy))forwarded.KnownProxies.Add(IPAddress.Parse(settings.KnownProxy));
app.UseForwardedHeaders(forwarded);
if(!app.Environment.IsDevelopment())app.UseHsts();
app.Use(async(context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["Content-Security-Policy"]="default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; style-src 'self'";
    context.Response.Headers["Referrer-Policy"]="no-referrer";
    if(!app.Environment.IsDevelopment() && !context.Request.IsHttps){context.Response.StatusCode=400;await context.Response.WriteAsync("HTTPS is required.");return;}
    await next();
});
app.UseExceptionHandler(error=>error.Run(async context=>{context.Response.StatusCode=500;await context.Response.WriteAsync("The licence server could not complete the request. Please retry or contact the owner.");}));
app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.MapGet("/",()=>Results.Redirect("/Admin"));app.MapGet("/health",()=>Results.Ok(new{status="ok"}));
app.MapGet("/.well-known/licence-public-key",(LicenceSigner signer)=>Results.Ok(new{publicKeyPem=signer.PublicKey}));
app.MapPost("/api/licences/check",(LicenceCheckRequest request,LicenceRepository repository)=>Results.Ok(repository.Check(request))).RequireRateLimiting("checks");
app.MapRazorPages();app.Run();
public partial class Program;
