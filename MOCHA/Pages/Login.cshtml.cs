using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Pages;

/// <summary>
/// 開発用ログインページ
/// </summary>
[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly IDevLoginService _loginService;
    private readonly IDevUserService _userService;
    private readonly DevAuthOptions _options;

    /// <summary>
    /// 入力データ
    /// </summary>
    [BindProperty]
    public DevLoginInput Input { get; set; } = new();

    /// <summary>
    /// リダイレクト先
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// 画面表示用のエラー
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// ログインモデルを初期化する
    /// </summary>
    /// <param name="loginService">ログインサービス</param>
    /// <param name="options">認証設定</param>
    public LoginModel(IDevLoginService loginService, IDevUserService userService, IOptions<DevAuthOptions> options)
    {
        _loginService = loginService;
        _userService = userService;
        _options = options.Value;
    }

    /// <summary>
    /// GET処理
    /// </summary>
    public IActionResult OnGet()
    {
        if (!_options.Enabled)
        {
            return Redirect("/");
        }

        ReturnUrl = NormalizeReturnUrl(ReturnUrl);
        return Page();
    }

    /// <summary>
    /// POST処理
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!_options.Enabled)
        {
            return Forbid();
        }

        ReturnUrl = NormalizeReturnUrl(ReturnUrl);

        if (!ModelState.IsValid)
        {
            Error = "入力を確認してください";
            return Page();
        }

        var lifetime = TimeSpan.FromHours(Math.Max(1, _options.ExpireHours));
        var user = await _userService.ValidateAsync(Input.Email, Input.Password);
        if (user == null)
        {
            Error = "メールアドレスまたはパスワードが正しくありません";
            return Page();
        }

        var principal = _loginService.CreatePrincipal(user.Email, user.Email);
        var properties = _loginService.CreateProperties(lifetime);

        await HttpContext.SignInAsync(DevAuthDefaults.scheme, principal, properties);
        return Redirect(ReturnUrl ?? "/");
    }

    private string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
    }
}
