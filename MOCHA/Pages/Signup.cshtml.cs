using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Linq;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Pages;

/// <summary>
/// サインアップページ
/// </summary>
[AllowAnonymous]
public sealed class SignupModel : PageModel
{
    private readonly IDevUserService _userService;
    private readonly IDevLoginService _loginService;
    private readonly DevAuthOptions _options;

    /// <summary>
    /// 入力データ
    /// </summary>
    [BindProperty]
    public DevSignUpInput Input { get; set; } = new();

    /// <summary>
    /// リダイレクト先
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// サインアップを初期化する
    /// </summary>
    public SignupModel(IDevUserService userService, IDevLoginService loginService, IOptions<DevAuthOptions> options)
    {
        _userService = userService;
        _loginService = loginService;
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
            var confirmErrors = ModelState[nameof(Input.ConfirmPassword)]?.Errors;
            var mismatch = confirmErrors?.Any(e => e.ErrorMessage.Contains("一致")) == true;
            Error = mismatch ? "パスワードが一致しません" : "入力を確認してください";
            return Page();
        }

        try
        {
            var user = await _userService.SignUpAsync(new DevSignUpInput
            {
                Email = Input.Email,
                Password = Input.Password,
                ConfirmPassword = Input.ConfirmPassword
            });

            var principal = _loginService.CreatePrincipal(user.Email, user.Email);
            var properties = _loginService.CreateProperties(TimeSpan.FromHours(Math.Max(1, _options.ExpireHours)));
            await HttpContext.SignInAsync(DevAuthDefaults.scheme, principal, properties);
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }

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
