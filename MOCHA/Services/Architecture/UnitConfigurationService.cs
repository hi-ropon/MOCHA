using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Auth;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// 装置ユニット構成ドメインサービス
/// </summary>
public sealed class UnitConfigurationService
{
    private readonly IUnitConfigurationRepository _repository;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<UnitConfigurationService> _logger;

    /// <summary>
    /// 依存受け取りによる初期化
    /// </summary>
    /// <param name="repository">リポジトリ</param>
    /// <param name="roleProvider">ロールプロバイダー</param>
    /// <param name="logger">ロガー</param>
    public UnitConfigurationService(IUnitConfigurationRepository repository, IUserRoleProvider roleProvider, ILogger<UnitConfigurationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roleProvider = roleProvider ?? throw new ArgumentNullException(nameof(roleProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// ユニット一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    public Task<IReadOnlyList<UnitConfiguration>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        _ = userId;
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<UnitConfiguration>>(Array.Empty<UnitConfiguration>());
        }

        return _repository.ListAsync(agentNumber.Trim(), cancellationToken);
    }

    /// <summary>
    /// ユニット追加
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">入力ドラフト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<UnitConfigurationResult> AddAsync(string userId, string agentNumber, UnitConfigurationDraft draft, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return UnitConfigurationResult.Fail("管理者または開発者のみ編集できます");
        }

        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return UnitConfigurationResult.Fail(validation.Error!);
        }

        var normalizedUserId = userId.Trim();
        var normalizedAgentNumber = agentNumber.Trim();
        var unit = UnitConfiguration.Create(normalizedUserId, normalizedAgentNumber, draft);

        var existing = await _repository.ListAsync(normalizedAgentNumber, cancellationToken);
        if (existing.Any(x => string.Equals(x.Name, unit.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return UnitConfigurationResult.Fail("同じユニット名が既に登録されています");
        }

        var saved = await _repository.AddAsync(unit, cancellationToken);
        _logger.LogInformation("ユニット構成を登録しました: {UnitName}", saved.Name);
        return UnitConfigurationResult.Success(saved);
    }

    /// <summary>
    /// ユニット更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="draft">更新内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<UnitConfigurationResult> UpdateAsync(string userId, string agentNumber, Guid unitId, UnitConfigurationDraft draft, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return UnitConfigurationResult.Fail("管理者または開発者のみ編集できます");
        }

        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return UnitConfigurationResult.Fail(validation.Error!);
        }

        var existing = await _repository.GetAsync(unitId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return UnitConfigurationResult.Fail("ユニット構成が見つかりません");
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return UnitConfigurationResult.Fail("別の装置エージェントに紐づくため更新できません");
        }

        var updated = existing.Update(draft);
        var siblings = await _repository.ListAsync(agentNumber.Trim(), cancellationToken);
        if (siblings.Any(x => x.Id != existing.Id && string.Equals(x.Name, updated.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return UnitConfigurationResult.Fail("同じユニット名が既に登録されています");
        }

        updated = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("ユニット構成を更新しました: {UnitName}", updated.Name);
        return UnitConfigurationResult.Success(updated);
    }

    /// <summary>
    /// ユニット削除
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除可否</returns>
    public async Task<bool> DeleteAsync(string userId, string agentNumber, Guid unitId, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return false;
        }

        var existing = await _repository.GetAsync(unitId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) ||
            !string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return false;
        }

        var deleted = await _repository.DeleteAsync(unitId, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation("ユニット構成を削除しました: {UnitId}", unitId);
        }

        return deleted;
    }

    private async Task<bool> HasEditPermissionAsync(string userId, CancellationToken cancellationToken)
    {
        var roles = new[]
        {
            UserRoleId.Predefined.Administrator.Value,
            UserRoleId.Predefined.Developer.Value
        };

        foreach (var role in roles)
        {
            if (await _roleProvider.IsInRoleAsync(userId, role, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static (bool IsValid, string? Error) Validate(string userId, string agentNumber, UnitConfigurationDraft draft)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, "ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return (false, "装置エージェントを選択してください");
        }

        var validation = draft.Validate();
        if (!validation.IsValid)
        {
            return validation;
        }

        return (true, null);
    }
}
