using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BebesNadadores.Web.Pages.Account;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;


    public LogoutModel(
        SignInManager<AppUser> signInManager)
    {
        _signInManager = signInManager;
    }


    public IActionResult OnGet()
    {
        return LocalRedirect("/");
    }


    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();

        return LocalRedirect(
            "/Account/Login");
    }
}