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

    public PlcConfigurationService(IPlcUnitRepository repository, ILogger<PlcConfigurationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<PlcUnit>>(Array.Empty<PlcUnit>());
        }

        return _repository.ListAsync(userId, agentNumber, cancellationToken);
    }

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
