using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCユニット設定を管理するドメインサービス
/// </summary>
internal sealed class PlcConfigurationService
{
    private const long _maxFileSizeBytesm = 10 * 1024 * 1024;
    private readonly IPlcUnitRepository _repository;
    private readonly ILogger<PlcConfigurationService> _logger;

    /// <summary>
    /// リポジトリとロガー注入による初期化
    /// </summary>
    /// <param name="repository">PLCユニットリポジトリ</param>
    /// <param name="logger">ロガー</param>
    public PlcConfigurationService(IPlcUnitRepository repository, ILogger<PlcConfigurationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// ユーザーとエージェントのユニット一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    public Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<PlcUnit>>(Array.Empty<PlcUnit>());
        }

        return _repository.ListAsync(userId, agentNumber, cancellationToken);
    }

    /// <summary>
    /// PLCユニット追加
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">登録内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PlcUnitResult> AddAsync(string userId, string agentNumber, PlcUnitDraft draft, CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PlcUnitResult.Fail(validation.Error!);
        }

        var unit = PlcUnit.Create(userId.Trim(), agentNumber.Trim(), draft);
        var saved = await _repository.AddAsync(unit, cancellationToken);
        _logger.LogInformation("PLCユニットを登録しました: {Name}", saved.Name);
        return PlcUnitResult.Success(saved);
    }

    /// <summary>
    /// PLCユニット更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="draft">更新内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PlcUnitResult> UpdateAsync(string userId, string agentNumber, Guid unitId, PlcUnitDraft draft, CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PlcUnitResult.Fail(validation.Error!);
        }

        var existing = await _repository.GetAsync(unitId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return PlcUnitResult.Fail("PLCユニットが見つかりません");
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return PlcUnitResult.Fail("別の装置エージェントに紐づくため更新できません");
        }

        var updated = existing.Update(draft);
        updated = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("PLCユニットを更新しました: {Name}", updated.Name);
        return PlcUnitResult.Success(updated);
    }

    /// <summary>
    /// PLCユニット削除
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    public async Task<bool> DeleteAsync(string userId, string agentNumber, Guid unitId, CancellationToken cancellationToken = default)
    {
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
            _logger.LogInformation("PLCユニットを削除しました: {Id}", unitId);
        }

        return deleted;
    }

    /// <summary>
    /// 入力値とドラフトのバリデーション
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">ユニットドラフト</param>
    /// <returns>検証結果</returns>
    private (bool IsValid, string? Error) Validate(string userId, string agentNumber, PlcUnitDraft draft)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, "ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return (false, "装置エージェントを選択してください");
        }

        var validation = draft.Validate(_maxFileSizeBytesm);
        if (!validation.IsValid)
        {
            return validation;
        }

        return (true, null);
    }
}
