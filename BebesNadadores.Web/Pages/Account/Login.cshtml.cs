using System.ComponentModel.DataAnnotations;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BebesNadadores.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;

    public LoginModel(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }


    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect("/");
        }

        return Page();
    }


    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();

        var usuario =
            await _userManager.FindByEmailAsync(email);

        if (usuario is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Usuario o contraseña incorrectos.");

            return Page();
        }

        if (!usuario.Activo)
        {
            ModelState.AddModelError(
                string.Empty,
                "El usuario se encuentra deshabilitado.");

            return Page();
        }

        var resultado =
            await _signInManager.PasswordSignInAsync(
                usuario,
                Input.Password,
                Input.Recordarme,
                lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl)
                && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return LocalRedirect("/");
        }

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "El usuario fue bloqueado temporalmente por varios intentos fallidos.");

            return Page();
        }

        ModelState.AddModelError(
            string.Empty,
            "Usuario o contraseña incorrectos.");

        return Page();
    }


    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Ingrese el email.")]
        [EmailAddress(ErrorMessage = "Ingrese un email válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese la contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool Recordarme { get; set; }
    }
}