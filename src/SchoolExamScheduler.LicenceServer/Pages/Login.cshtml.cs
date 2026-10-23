using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace SchoolExamScheduler.LicenceServer.Pages;
[AllowAnonymous,EnableRateLimiting("login")]
public sealed class LoginModel(ServerOptions options) : PageModel
{
    [BindProperty] public string? Username {get;set;}
    [BindProperty] public string? Password {get;set;}
    public async Task<IActionResult> OnPostAsync()
    {
        var valid=AdminPasswords.Verify(Password??"",options.AdminPasswordHash);
        if(!valid || !string.Equals(Username,options.AdminUsername,StringComparison.Ordinal)){ModelState.AddModelError("","Sign-in failed.");return Page();}
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name,options.AdminUsername)],CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage("/Admin/Index");
    }
}
