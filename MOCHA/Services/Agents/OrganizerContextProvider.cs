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
    private const int _maxUnits = 3;
    private const int _maxModules = 4;
    private const int _maxFunctionBlocks = 4;
    private const int _maxDrawings = 5;
    private readonly IPlcUnitRepository _plcUnitRepository;
    private readonly DrawingCatalog _drawingCatalog;
    private readonly ILogger<OrganizerContextProvider> _logger;

    /// <summary>
    /// 依存注入による初期化
    /// </summary>
    public OrganizerContextProvider(
        IPlcUnitRepository plcUnitRepository,
        DrawingCatalog drawingCatalog,
        ILogger<OrganizerContextProvider> logger)
    {
        _plcUnitRepository = plcUnitRepository ?? throw new ArgumentNullException(nameof(plcUnitRepository));
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
        var units = await _plcUnitRepository.ListAsync(userId, agentNumber, cancellationToken);
        if (units.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var ordered = units
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.CreatedAt)
            .ToList();

        foreach (var unit in ordered.Take(_maxUnits))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = FormatUnitHeader(unit);
            sb.AppendLine(header);

            var modules = unit.Modules?.Take(_maxModules).ToList() ?? new List<PlcUnitModule>();
            if (modules.Count > 0)
            {
                var moduleText = string.Join(", ", modules.Select(m => string.IsNullOrWhiteSpace(m.Specification) ? m.Name : $"{m.Name}({m.Specification})"));
                sb.AppendLine($"  モジュール: {moduleText}");
                if ((unit.Modules?.Count ?? 0) > modules.Count)
                {
                    sb.AppendLine($"  …他{(unit.Modules!.Count - modules.Count)}モジュール");
                }
            }

            var blocks = unit.FunctionBlocks?.Take(_maxFunctionBlocks).ToList() ?? new List<FunctionBlock>();
            if (blocks.Count > 0)
            {
                var blockText = string.Join(", ", blocks.Select(b => $"{b.Name}(safe:{b.SafeName})"));
                sb.AppendLine($"  FB: {blockText}");
                if ((unit.FunctionBlocks?.Count ?? 0) > blocks.Count)
                {
                    sb.AppendLine($"  …他{(unit.FunctionBlocks!.Count - blocks.Count)}ファンクションブロック");
                }
            }

            var files = CollectFiles(unit);
            if (files.Count > 0)
            {
                sb.AppendLine($"  ファイル: {string.Join(", ", files)}");
            }
        }

        if (ordered.Count > _maxUnits)
        {
            sb.AppendLine($"…他{ordered.Count - _maxUnits}ユニット");
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
        foreach (var file in ordered.Take(_maxDrawings))
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(FormatDrawingLine(file));
        }

        if (ordered.Count > _maxDrawings)
        {
            sb.AppendLine($"…他{ordered.Count - _maxDrawings}図面");
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
}
