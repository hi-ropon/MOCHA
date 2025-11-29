using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MOCHA.Pages;

/// <summary>
/// アクセス拒否ページ
/// </summary>
[AllowAnonymous]
public sealed class DeniedModel : PageModel
{
    /// <summary>
    /// GET処理
    /// </summary>
    public void OnGet()
    {
    }
}
