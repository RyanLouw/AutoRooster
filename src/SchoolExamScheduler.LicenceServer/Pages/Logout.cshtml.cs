using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace SchoolExamScheduler.LicenceServer.Pages;
[Authorize]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync(){await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);return RedirectToPage("/Login");}
}
