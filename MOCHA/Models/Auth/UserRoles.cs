using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Models.Auth;

/// <summary>
/// ユーザーに付与するロール ID（大文字で正規化）。
/// </summary>
public readonly record struct UserRoleId
{
    public string Value { get; }

    private UserRoleId(string value)
    {
        Value = value;
    }

    public static UserRoleId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ロール名が空です。", nameof(value));
        }

        return new UserRoleId(value.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;

    public static class Predefined
    {
        public static UserRoleId Administrator => From("Administrator");
        public static UserRoleId Developer => From("Developer");
        public static UserRoleId Operator => From("Operator");

        // 将来拡張: 開発部門・メカ設計・エレキ設計・製造など
        public static UserRoleId Development => From("Development");
        public static UserRoleId MechanicalDesign => From("MechanicalDesign");
        public static UserRoleId ElectricalDesign => From("ElectricalDesign");
        public static UserRoleId Manufacturing => From("Manufacturing");
    }
}

public interface IUserRoleProvider
{
    Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default);

    Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default);

    Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
}
