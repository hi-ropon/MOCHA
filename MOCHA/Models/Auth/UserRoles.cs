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

    /// <summary>
    /// 任意の文字列を大文字化してロールIDを生成する。
    /// </summary>
    /// <param name="value">ロール名。</param>
    /// <returns>正規化されたロールID。</returns>
    /// <exception cref="ArgumentException">空または空白のみの場合にスロー。</exception>
    public static UserRoleId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ロール名が空です。", nameof(value));
        }

        return new UserRoleId(value.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// 内部値をそのまま文字列として返す。
    /// </summary>
    /// <returns>ロールIDの文字列表現。</returns>
    public override string ToString() => Value;

    /// <summary>
    /// よく使うロールの定義集。
    /// </summary>
    public static class Predefined
    {
        /// <summary>管理者ロール。</summary>
        public static UserRoleId Administrator => From("Administrator");
        /// <summary>開発者ロール。</summary>
        public static UserRoleId Developer => From("Developer");
        /// <summary>オペレーター（運用）ロール。</summary>
        public static UserRoleId Operator => From("Operator");

        // 将来拡張: 開発部門・メカ設計・エレキ設計・製造など
        /// <summary>開発部門共通ロール。</summary>
        public static UserRoleId Development => From("Development");
        /// <summary>機械設計ロール。</summary>
        public static UserRoleId MechanicalDesign => From("MechanicalDesign");
        /// <summary>電気設計ロール。</summary>
        public static UserRoleId ElectricalDesign => From("ElectricalDesign");
        /// <summary>製造ロール。</summary>
        public static UserRoleId Manufacturing => From("Manufacturing");
    }
}

/// <summary>
/// ユーザーへのロール付与・解除を抽象化する。
/// </summary>
public interface IUserRoleProvider
{
    /// <summary>
    /// ユーザーに紐づくロールを取得する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>割り当て済みロールの一覧。</returns>
    Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーにロールを付与する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">付与するロール。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーからロールを剥奪する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">削除するロール。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 特定ロールを保持しているか判定する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">確認したいロール名。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保持していれば true。</returns>
    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
}
