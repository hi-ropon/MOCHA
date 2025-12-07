using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ゲートウェイ設定を管理するドメインサービス
/// </summary>
internal sealed class GatewayConfigurationService
{
    private readonly IGatewaySettingRepository _repository;
    private readonly ILogger<GatewayConfigurationService> _logger;

    public GatewayConfigurationService(IGatewaySettingRepository repository, ILogger<GatewayConfigurationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>設定取得</summary>
    public Task<GatewaySetting?> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<GatewaySetting?>(null);
        }

        return _repository.GetAsync(userId.Trim(), agentNumber.Trim(), cancellationToken);
    }

    /// <summary>設定保存</summary>
    public async Task<GatewaySettingResult> SaveAsync(string userId, string agentNumber, GatewaySettingDraft draft, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return GatewaySettingResult.Fail("ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return GatewaySettingResult.Fail("装置エージェントを選択してください");
        }

        var validation = draft.Validate();
        if (!validation.IsValid)
        {
            return GatewaySettingResult.Fail(validation.Error!);
        }

        try
        {
            var existing = await _repository.GetAsync(userId.Trim(), agentNumber.Trim(), cancellationToken);
            var setting = existing is null
                ? GatewaySetting.Create(userId, agentNumber, draft)
                : existing.Update(draft);

            var saved = await _repository.UpsertAsync(setting, cancellationToken);
            _logger.LogInformation("ゲートウェイ設定を保存しました: {Host}:{Port}", saved.Host, saved.Port);
            return GatewaySettingResult.Success(saved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ゲートウェイ設定の保存に失敗しました。");
            return GatewaySettingResult.Fail("ゲートウェイ設定の保存に失敗しました");
        }
    }
}
