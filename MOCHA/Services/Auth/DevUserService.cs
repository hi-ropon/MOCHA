using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// 開発用ユーザー管理の実装
/// </summary>
internal sealed class DevUserService : IDevUserService
{
    private readonly ChatDbContext _db;
    private readonly IPasswordHasher<DevUserEntity> _passwordHasher;
    private readonly IUserRoleProvider _roleProvider;

    /// <summary>
    /// 依存を受け取って初期化する
    /// </summary>
    /// <param name="db">DbContext</param>
    /// <param name="passwordHasher">パスワードハッシュ生成器</param>
    /// <param name="roleProvider">ロール割り当てプロバイダー</param>
    public DevUserService(ChatDbContext db, IPasswordHasher<DevUserEntity> passwordHasher, IUserRoleProvider roleProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _roleProvider = roleProvider;
    }

    /// <inheritdoc />
    public async Task<DevUserEntity> SignUpAsync(DevSignUpInput input, CancellationToken cancellationToken = default)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var email = input.Email.Trim().ToLowerInvariant();
        var exists = await _db.Set<DevUserEntity>().AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("同じメールアドレスのユーザーが既に存在します");
        }

        var entity = new DevUserEntity
        {
            Email = email,
            DisplayName = email,
            CreatedAt = DateTime.UtcNow
        };
        entity.PasswordHash = _passwordHasher.HashPassword(entity, input.Password);

        _db.Set<DevUserEntity>().Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _roleProvider.AssignAsync(entity.Email, UserRoleId.Predefined.Operator, cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public async Task<DevUserEntity?> ValidateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Set<DevUserEntity>().SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
