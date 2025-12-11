using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Services.Agents;

/// <summary>
/// PLCエージェントに渡す接続コンテキストの組み立て
/// </summary>
public sealed class PlcAgentContextProvider : IPlcAgentContextProvider
{
    private const int _maxUnits = 5;
    private readonly IPlcUnitRepository _plcUnitRepository;
    private readonly IGatewaySettingRepository _gatewaySettingRepository;
    private readonly ILogger<PlcAgentContextProvider> _logger;

    /// <summary>
    /// 依存関係の注入による初期化
    /// </summary>
    public PlcAgentContextProvider(
        IPlcUnitRepository plcUnitRepository,
        IGatewaySettingRepository gatewaySettingRepository,
        ILogger<PlcAgentContextProvider> logger)
    {
        _plcUnitRepository = plcUnitRepository ?? throw new ArgumentNullException(nameof(plcUnitRepository));
        _gatewaySettingRepository = gatewaySettingRepository ?? throw new ArgumentNullException(nameof(gatewaySettingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PlcAgentContext> BuildAsync(string? userId, string? agentNumber, Guid? plcUnitId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return PlcAgentContext.Empty;
        }

        try
        {
            var normalizedAgent = agentNumber.Trim();
            var gateway = await _gatewaySettingRepository.GetAsync(userId, normalizedAgent, cancellationToken);
            var units = await _plcUnitRepository.ListAsync(normalizedAgent, cancellationToken);
            if (plcUnitId is not null)
            {
                units = units.Where(u => u.Id == plcUnitId.Value).ToList();
            }

            var orderedUnits = units
                .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.CreatedAt)
                .Take(_maxUnits)
                .Select(u => BuildUnit(u, gateway))
                .ToList();

            return new PlcAgentContext(gateway?.Host, gateway?.Port, orderedUnits);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PLCエージェントコンテキスト生成に失敗しました。");
            return PlcAgentContext.Empty;
        }
    }

    private static PlcAgentUnit BuildUnit(PlcUnit unit, GatewaySetting? gateway)
    {
        var host = string.IsNullOrWhiteSpace(unit.GatewayHost) ? gateway?.Host : unit.GatewayHost;
        var port = unit.GatewayPort ?? gateway?.Port;
        return new PlcAgentUnit(unit.Id, unit.Name, unit.IpAddress, unit.Port, host, port);
    }
}
