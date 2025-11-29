using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MOCHA.Models.Auth;

namespace MOCHA.Pages;

/// <summary>
/// ログアウトページ
/// </summary>
[Authorize]
public sealed class LogoutModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Confirm { get; set; }

    /// <summary>
    /// GET処理
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (Confirm)
        {
            await HttpContext.SignOutAsync(DevAuthDefaults.scheme);
            return Redirect("/login");
        }

        return Page();
    }

    /// <summary>
    /// POST処理
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(DevAuthDefaults.scheme);
        return Redirect("/login");
    }
}
