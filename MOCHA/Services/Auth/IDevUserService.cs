using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// 開発用ユーザー管理サービス
/// </summary>
public interface IDevUserService
{
    /// <summary>
    /// ユーザーを登録する
    /// </summary>
    /// <param name="input">サインアップ入力</param>
    /// <param name="cancellationToken">キャンセル</param>
    /// <returns>登録したユーザー</returns>
    Task<DevUserEntity> SignUpAsync(DevSignUpInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// メールアドレスとパスワードで検証する
    /// </summary>
    /// <param name="email">メールアドレス</param>
    /// <param name="password">パスワード</param>
    /// <param name="cancellationToken">キャンセル</param>
    /// <returns>一致したユーザー。見つからない場合は null</returns>
    Task<DevUserEntity?> ValidateAsync(string email, string password, CancellationToken cancellationToken = default);
}
