using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Domain;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>装置エージェントごとのサブエージェント設定を管理するサービス</summary>
public sealed class AgentDelegationSettingService
{
    private readonly IAgentDelegationSettingRepository _repository;
    private readonly AgentDelegationOptions _options;
    private readonly ILogger<AgentDelegationSettingService> _logger;
    private readonly IReadOnlyCollection<string> _baseAllowed;

    public AgentDelegationSettingService(
        IAgentDelegationSettingRepository repository,
        IOptions<AgentDelegationOptions> optionsAccessor,
        ILogger<AgentDelegationSettingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = optionsAccessor?.Value ?? new AgentDelegationOptions();
        _baseAllowed = ResolveBaseAllowed(_options);
    }

    /// <summary>設定取得（未設定の場合は既定値）</summary>
    public async Task<AgentDelegationSetting> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        var normalizedAgent = NormalizeAgent(agentNumber);
        var normalizedUser = (userId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedAgent) || string.IsNullOrWhiteSpace(normalizedUser))
        {
            return CreateDefault(normalizedAgent);
        }

        var found = await _repository.GetAsync(normalizedUser, normalizedAgent, cancellationToken);
        if (found is null)
        {
            return CreateDefault(normalizedAgent);
        }

        return NormalizeSetting(found);
    }

    /// <summary>設定保存</summary>
    public async Task<AgentDelegationSettingResult> SaveAsync(string userId, string agentNumber, AgentDelegationSettingDraft draft, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return AgentDelegationSettingResult.Fail("ユーザーIDを取得できませんでした");
        }

        var normalizedAgent = NormalizeAgent(agentNumber);
        if (string.IsNullOrWhiteSpace(normalizedAgent))
        {
            return AgentDelegationSettingResult.Fail("装置エージェントを選択してください");
        }

        var normalizedList = NormalizeSubAgents(draft?.AllowedSubAgents);
        if (normalizedList.Count == 0)
        {
            return AgentDelegationSettingResult.Fail("サブエージェントを1つ以上選択してください");
        }

        try
        {
            var saved = await _repository.UpsertAsync(userId.Trim(), new AgentDelegationSetting(normalizedAgent, normalizedList), cancellationToken);
            return AgentDelegationSettingResult.Success(NormalizeSetting(saved));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サブエージェント設定の保存に失敗しました。");
            return AgentDelegationSettingResult.Fail("サブエージェント設定の保存に失敗しました");
        }
    }

    private AgentDelegationSetting CreateDefault(string agentNumber)
    {
        return new AgentDelegationSetting(agentNumber, _baseAllowed);
    }

    private AgentDelegationSetting NormalizeSetting(AgentDelegationSetting setting)
    {
        var normalized = NormalizeSubAgents(setting.AllowedSubAgents);
        return new AgentDelegationSetting(setting.AgentNumber, normalized.Count > 0 ? normalized : _baseAllowed);
    }

    private static string NormalizeAgent(string? agentNumber) => (agentNumber ?? string.Empty).Trim();

    private IReadOnlyCollection<string> NormalizeSubAgents(IReadOnlyCollection<string>? candidates)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in candidates ?? Array.Empty<string>())
        {
            if (SubAgentOptions.AllowedIds.Contains(id) && _baseAllowed.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                set.Add(id);
            }
        }

        return set.ToList();
    }

    private static IReadOnlyCollection<string> ResolveBaseAllowed(AgentDelegationOptions options)
    {
        var edges = options?.AllowedEdges ?? AgentDelegationOptions.CreateDefaultEdges();
        if (edges.TryGetValue("organizer", out var allowed) && allowed is not null)
        {
            return allowed
                .Where(a => SubAgentOptions.AllowedIds.Contains(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return SubAgentOptions.AllowedIds.ToList();
    }
}
