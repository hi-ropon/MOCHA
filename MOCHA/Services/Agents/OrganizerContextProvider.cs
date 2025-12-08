using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Models.Architecture;
using MOCHA.Models.Drawings;
using MOCHA.Services.Architecture;
using MOCHA.Services.Drawings;

namespace MOCHA.Services.Agents;

/// <summary>
/// Organizer に渡すアーキテクチャ/図面コンテキストの組み立て
/// </summary>
public sealed class OrganizerContextProvider : IOrganizerContextProvider
{
    private const int _maxPcSettings = 3;
    private const int _maxRepositoryUrls = 3;
    private readonly IPcSettingRepository _pcSettingRepository;
    private readonly IPlcUnitRepository _plcUnitRepository;
    private readonly IGatewaySettingRepository _gatewaySettingRepository;
    private readonly IUnitConfigurationRepository _unitConfigurationRepository;
    private readonly DrawingCatalog _drawingCatalog;
    private readonly ILogger<OrganizerContextProvider> _logger;

    /// <summary>
    /// 依存注入による初期化
    /// </summary>
    public OrganizerContextProvider(
        IPcSettingRepository pcSettingRepository,
        IPlcUnitRepository plcUnitRepository,
        IGatewaySettingRepository gatewaySettingRepository,
        IUnitConfigurationRepository unitConfigurationRepository,
        DrawingCatalog drawingCatalog,
        ILogger<OrganizerContextProvider> logger)
    {
        _pcSettingRepository = pcSettingRepository ?? throw new ArgumentNullException(nameof(pcSettingRepository));
        _plcUnitRepository = plcUnitRepository ?? throw new ArgumentNullException(nameof(plcUnitRepository));
        _gatewaySettingRepository = gatewaySettingRepository ?? throw new ArgumentNullException(nameof(gatewaySettingRepository));
        _unitConfigurationRepository = unitConfigurationRepository ?? throw new ArgumentNullException(nameof(unitConfigurationRepository));
        _drawingCatalog = drawingCatalog ?? throw new ArgumentNullException(nameof(drawingCatalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<OrganizerContext> BuildAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return OrganizerContext.Empty;
        }

        try
        {
            var architecture = await BuildArchitectureAsync(userId, agentNumber, cancellationToken);
            var drawings = await BuildDrawingsAsync(userId, agentNumber, cancellationToken);
            return new OrganizerContext(architecture, drawings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Organizer コンテキスト生成に失敗しました。");
            return OrganizerContext.Empty;
        }
    }

    private async Task<string> BuildArchitectureAsync(string userId, string agentNumber, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        var gateway = await _gatewaySettingRepository.GetAsync(userId, agentNumber, cancellationToken);
        if (gateway is not null)
        {
            sb.AppendLine($"- ゲートウェイ: {gateway.Host}:{gateway.Port}");
        }

        var pcSettings = await _pcSettingRepository.ListAsync(userId, agentNumber, cancellationToken);
        if (pcSettings.Count > 0)
        {
            var orderedPc = pcSettings
                .OrderBy(p => p.Os, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.CreatedAt)
                .ToList();

            foreach (var pc in orderedPc.Take(_maxPcSettings))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine(FormatPcLine(pc));
            }

            if (orderedPc.Count > _maxPcSettings)
            {
                sb.AppendLine($"…他{orderedPc.Count - _maxPcSettings}台のPC設定");
            }
        }

        var units = await _plcUnitRepository.ListAsync(userId, agentNumber, cancellationToken);
        if (units.Count > 0)
        {
            var orderedPlc = units
                .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.CreatedAt)
                .ToList();

            foreach (var unit in orderedPlc)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var header = FormatUnitHeader(unit);
                sb.AppendLine(header);

                var modules = unit.Modules?.ToList() ?? new List<PlcUnitModule>();
                if (modules.Count > 0)
                {
                    var moduleText = string.Join(", ", modules.Select(m => string.IsNullOrWhiteSpace(m.Specification) ? m.Name : $"{m.Name}({m.Specification})"));
                    sb.AppendLine($"  モジュール: {moduleText}");
                }

                var blocks = unit.FunctionBlocks?.ToList() ?? new List<FunctionBlock>();
                if (blocks.Count > 0)
                {
                    var blockText = string.Join(", ", blocks.Select(b => $"{b.Name}(safe:{b.SafeName})"));
                    sb.AppendLine($"  FB: {blockText}");
                }

                var files = CollectFiles(unit);
                if (files.Count > 0)
                {
                    sb.AppendLine($"  ファイル: {string.Join(", ", files)}");
                }
            }
        }

        var unitConfigurations = await _unitConfigurationRepository.ListAsync(userId, agentNumber, cancellationToken);
        foreach (var config in unitConfigurations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(FormatUnitConfiguration(config));
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string> BuildDrawingsAsync(string userId, string agentNumber, CancellationToken cancellationToken)
    {
        var files = await _drawingCatalog.ListAsync(userId, agentNumber, cancellationToken);
        if (files.Count == 0)
        {
            return string.Empty;
        }

        var ordered = files
            .OrderByDescending(f => f.Document.UpdatedAt)
            .ThenBy(f => f.Document.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        foreach (var file in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(FormatDrawingLine(file));
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatUnitHeader(PlcUnit unit)
    {
        var manufacturer = unit.Manufacturer;
        var model = string.IsNullOrWhiteSpace(unit.Model) ? string.Empty : $" {unit.Model}";
        var role = string.IsNullOrWhiteSpace(unit.Role) ? "役割未設定" : unit.Role;
        var ip = string.IsNullOrWhiteSpace(unit.IpAddress) ? "-" : unit.IpAddress;
        var port = unit.Port?.ToString(CultureInfo.InvariantCulture) ?? "-";
        return $"- ユニット: {unit.Name} ({manufacturer}{model}) role={role} ip={ip} port={port}";
    }

    private string FormatPcLine(PcSetting pc)
    {
        var role = string.IsNullOrWhiteSpace(pc.Role) ? "役割未設定" : pc.Role;
        var repos = pc.RepositoryUrls?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new List<string>();
        var repoDisplay = "repos:-";
        if (repos.Count > 0)
        {
            var take = repos.Take(_maxRepositoryUrls).ToList();
            repoDisplay = $"repos:{string.Join(", ", take)}";
            if (repos.Count > take.Count)
            {
                repoDisplay += $" …他{repos.Count - take.Count}件";
            }
        }

        return $"- PC: {pc.Os} role={role} {repoDisplay}";
    }

    private static List<string> CollectFiles(PlcUnit unit)
    {
        var files = new List<string>();
        if (unit.CommentFile is not null)
        {
            files.Add($"コメント:{NormalizeFileName(unit.CommentFile)}");
        }

        var programs = unit.ProgramFiles?.ToList() ?? new List<PlcFileUpload>();
        if (programs.Count > 0)
        {
            var programNames = string.Join(", ", programs.Select(p => NormalizeFileName(p)));
            files.Add($"プログラム:{programNames}");
        }

        return files;
    }

    private string FormatDrawingLine(DrawingFile file)
    {
        var doc = file.Document;
        var description = string.IsNullOrWhiteSpace(doc.Description) ? string.Empty : $" - {doc.Description}";
        var size = FormatSize(doc.FileSize);
        var status = !file.Exists
            ? "ファイル未発見"
            : IsPreviewable(file.Extension) ? "読取可" : "メタのみ";
        var updated = doc.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"- drawing:{doc.Id} {doc.FileName}{description} ({size}, 更新:{updated}, {status})";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:F0}KB";
        }

        var mb = kb / 1024d;
        return $"{mb:F1}MB";
    }

    private static bool IsPreviewable(string extension)
    {
        var ext = extension?.ToLowerInvariant() ?? string.Empty;
        return ext is ".pdf" or ".txt" or ".log" or ".md" or ".csv";
    }

    private static string NormalizeFileName(PlcFileUpload file)
    {
        return string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName!;
    }

    private static string FormatUnitConfiguration(UnitConfiguration config)
    {
        var description = string.IsNullOrWhiteSpace(config.Description) ? "-" : config.Description;
        var devices = config.Devices?
            .OrderBy(d => d.Order)
            .Select(FormatDevice)
            .ToList() ?? new List<string>();
        var deviceText = devices.Count == 0 ? "-" : string.Join(", ", devices);

        return $"- 装置ユニット: {config.Name} desc:{description}\n  機器: {deviceText}";
    }

    private static string FormatDevice(UnitDevice device)
    {
        var specParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(device.Model))
        {
            specParts.Add(device.Model);
        }

        if (!string.IsNullOrWhiteSpace(device.Maker))
        {
            specParts.Add(device.Maker);
        }

        var spec = specParts.Count > 0 ? $"({string.Join("/", specParts)})" : string.Empty;
        var description = string.IsNullOrWhiteSpace(device.Description) ? string.Empty : $" desc:{device.Description}";
        return $"{device.Name}{spec}{description}";
    }
}
