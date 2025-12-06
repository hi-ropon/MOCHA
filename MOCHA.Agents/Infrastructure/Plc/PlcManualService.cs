using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// PLC向けのマニュアル検索ヘルパー
/// </summary>
public sealed class PlcManualService
{
    private readonly IManualStore _manualStore;

    public PlcManualService(IManualStore manualStore)
    {
        _manualStore = manualStore ?? throw new ArgumentNullException(nameof(manualStore));
    }

    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var hits = await _manualStore.SearchAsync("plcAgent", query, null, cancellationToken);
        if (hits.Count == 0)
        {
            return $"マニュアル候補が見つかりませんでした: {query}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"「{query}」に関するマニュアル候補:");
        foreach (var hit in hits.Take(5))
        {
            sb.AppendLine($"- {hit.Title} ({hit.RelativePath}) score={hit.Score:F2}");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> SearchInstructionAsync(string instructionName, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(instructionName, cancellationToken);
    }

    public async Task<string> GetCommandOverviewAsync(CancellationToken cancellationToken = default)
    {
        var hits = await _manualStore.SearchAsync("plcAgent", "命令一覧", null, cancellationToken);
        var top = hits.FirstOrDefault();
        if (top is null)
        {
            return "命令一覧の概要が見つかりませんでした。";
        }

        var content = await _manualStore.ReadAsync("plcAgent", top.RelativePath, maxBytes: 600, cancellationToken: cancellationToken);
        if (content is null)
        {
            return $"命令一覧の読取に失敗しました: {top.RelativePath}";
        }

        return JsonSerializer.Serialize(new
        {
            title = top.Title,
            path = top.RelativePath,
            preview = content.Content
        });
    }
}
