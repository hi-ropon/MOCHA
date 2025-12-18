using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Auth;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PC設定を管理するドメインサービス
/// </summary>
internal sealed class PcConfigurationService
{
    private readonly IPcSettingRepository _repository;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<PcConfigurationService> _logger;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    /// <param name="repository">設定リポジトリ</param>
    /// <param name="roleProvider">ロールプロバイダー</param>
    /// <param name="logger">ロガー</param>
    public PcConfigurationService(IPcSettingRepository repository, IUserRoleProvider roleProvider, ILogger<PcConfigurationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roleProvider = roleProvider ?? throw new ArgumentNullException(nameof(roleProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 設定一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>設定一覧</returns>
    public Task<IReadOnlyList<PcSetting>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        _ = userId;
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<PcSetting>>(Array.Empty<PcSetting>());
        }

        return _repository.ListAsync(agentNumber, cancellationToken);
    }

    /// <summary>
    /// 設定追加
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">入力値</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PcSettingResult> AddAsync(string userId, string agentNumber, PcSettingDraft draft, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return PcSettingResult.Fail("管理者または開発者のみ編集できます");
        }

        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PcSettingResult.Fail(validation.Error!);
        }

        var setting = PcSetting.Create(userId.Trim(), agentNumber.Trim(), draft);
        var saved = await _repository.AddAsync(setting, cancellationToken);
        _logger.LogInformation("PC設定を登録しました: {Os}", saved.Os);
        return PcSettingResult.Success(saved);
    }

    /// <summary>
    /// 設定更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="settingId">設定ID</param>
    /// <param name="draft">更新値</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PcSettingResult> UpdateAsync(string userId, string agentNumber, Guid settingId, PcSettingDraft draft, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return PcSettingResult.Fail("管理者または開発者のみ編集できます");
        }

        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PcSettingResult.Fail(validation.Error!);
        }

        var existing = await _repository.GetAsync(settingId, cancellationToken);
        if (existing is null)
        {
            return PcSettingResult.Fail("PC設定が見つかりません");
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return PcSettingResult.Fail("別の装置エージェントに紐づくため更新できません");
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) &&
            !await HasEditPermissionAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return PcSettingResult.Fail("管理者または開発者のみ編集できます");
        }

        var updated = existing.Update(draft);
        updated = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("PC設定を更新しました: {Os}", updated.Os);
        return PcSettingResult.Success(updated);
    }

    /// <summary>
    /// 設定削除
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="settingId">設定ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    public async Task<bool> DeleteAsync(string userId, string agentNumber, Guid settingId, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return false;
        }

        var existing = await _repository.GetAsync(settingId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) &&
            !await HasEditPermissionAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var deleted = await _repository.DeleteAsync(settingId, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation("PC設定を削除しました: {Id}", settingId);
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

    private static (bool IsValid, string? Error) Validate(string userId, string agentNumber, PcSettingDraft draft)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, "ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return (false, "装置エージェントを選択してください");
        }

        var result = draft.Validate();
        if (!result.IsValid)
        {
            return result;
        }

        return (true, null);
    }
}
