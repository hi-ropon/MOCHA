namespace MOCHA.Services.Auth;

/// <summary>
/// ユーザー検索の結果
/// </summary>
public sealed record UserLookupResult(string UserId, string DisplayName);

/// <summary>
/// ユーザー情報を検索するサービス
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// 指定された識別子からユーザー情報を取得
    /// </summary>
    /// <param name="identifier">入力されたユーザー識別子</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>見つかればユーザー情報</returns>
    Task<UserLookupResult?> FindByIdentifierAsync(string? identifier, CancellationToken cancellationToken = default);
}

internal sealed class UserLookupService : IUserLookupService
{
    private readonly IEnumerable<IUserLookupStrategy> _strategies;

    public UserLookupService(IEnumerable<IUserLookupStrategy> strategies)
    {
        _strategies = strategies;
    }

    public async Task<UserLookupResult?> FindByIdentifierAsync(string? identifier, CancellationToken cancellationToken = default)
    {
        var normalized = RoleSettingsService.NormalizeUserId(identifier);
        if (normalized is null)
        {
            return null;
        }

        foreach (var strategy in _strategies)
        {
            var result = await strategy.LookupAsync(normalized, cancellationToken);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}

internal interface IUserLookupStrategy
{
    Task<UserLookupResult?> LookupAsync(string normalizedIdentifier, CancellationToken cancellationToken);
}
