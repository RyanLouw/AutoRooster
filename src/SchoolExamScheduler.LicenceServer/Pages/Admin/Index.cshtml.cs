using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.LicenceServer.Pages.Admin;
public sealed class IndexModel(LicenceRepository repository,TimeProvider clock) : PageModel
{
    public IReadOnlyList<SchoolLicence> Licences {get;private set;}=[];
    public IReadOnlyList<LicenceAudit> History {get;private set;}=[];
    public DateTimeOffset Now=>clock.GetUtcNow();
    public string DefaultPaidThrough=>DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(2)).DateTime).AddMonths(1).ToString("yyyy-MM-dd");
    [TempData] public string? NewKey {get;set;}
    [TempData] public string? Notice {get;set;}
    [BindProperty] public string? School {get;set;}
    [BindProperty] public string? Contact {get;set;}
    [BindProperty] public string? PaidThrough {get;set;}
    [BindProperty] public int MaximumDevices {get;set;}=2;
    [BindProperty] public Guid Id {get;set;}
    [BindProperty] public long Revision {get;set;}
    [BindProperty] public bool Enabled {get;set;}
    [BindProperty] public string? BankReference {get;set;}
    [BindProperty] public string? Note {get;set;}
    private string Actor=>User.Identity!.Name!;
    private DateOnly Date()=>DateOnly.TryParseExact(PaidThrough,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:throw new ArgumentException("Choose a valid paid-through date.");
    public void OnGet(Guid? history){Load(history);}
    private void Load(Guid? history=null){Licences=repository.List();if(history.HasValue)History=repository.Audit(history.Value);}
    private IActionResult Update(Action change,string notice)
    {
        try{change();Notice=notice;return RedirectToPage();}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException){ModelState.AddModelError("",e.Message);Load();return Page();}
    }
    public IActionResult OnPostCreate()=>Update(()=>{var created=repository.Create(School??"",Contact,Date(),MaximumDevices,Actor);NewKey=created.Key;},"School licence created. Copy the key below and give it to the school.");
    public IActionResult OnPostExtend()=>Update(()=>repository.Extend(Id,Revision,Date(),BankReference??"",Note,Actor),"Paid-through date extended. If the licence is disabled, use Enable separately.");
    public IActionResult OnPostSwitch()=>Update(()=>repository.SetEnabled(Id,Revision,Enabled,Actor),Enabled?"Licence enabled.":"Licence switched off. Online checks stop access; an offline signed allowance can remain valid for up to 24 hours.");
    public IActionResult OnPostReplaceKey()=>Update(()=>NewKey=repository.RotateKey(Id,Revision,Actor),"Key replaced. Give the new key to the school; the old key is no longer accepted online.");
    public IActionResult OnPostResetDevices()=>Update(()=>repository.ResetInstallations(Id,Revision,Actor),"Activation slots released. Replace the key as well if an old computer must not activate again.");
}
