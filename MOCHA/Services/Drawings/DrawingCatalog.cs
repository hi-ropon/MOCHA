using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面のファイル参照を解決するカタログ
/// </summary>
public sealed class DrawingCatalog
{
    private readonly IDrawingRepository _repository;
    private readonly DrawingStorageOptions _options;

    /// <summary>
    /// 依存サービスを注入して初期化
    /// </summary>
    /// <param name="repository">図面リポジトリ</param>
    /// <param name="options">ストレージ設定</param>
    public DrawingCatalog(IDrawingRepository repository, IOptions<DrawingStorageOptions> options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 単一図面を取得する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="drawingId">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面ファイル参照</returns>
    public async Task<DrawingFile?> FindAsync(string? agentNumber, Guid drawingId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetAsync(drawingId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!string.Equals(document.AgentNumber ?? string.Empty, agentNumber ?? string.Empty, StringComparison.Ordinal))
        {
            return null;
        }

        return BuildFile(document);
    }

    /// <summary>
    /// 図面一覧を取得する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面ファイル参照一覧</returns>
    public async Task<IReadOnlyList<DrawingFile>> ListAsync(string? agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return Array.Empty<DrawingFile>();
        }

        var documents = await _repository.ListAsync(agentNumber, cancellationToken);
        return documents
            .Select(BuildFile)
            .ToList();
    }

    private DrawingFile BuildFile(DrawingDocument document)
    {
        var root = ResolveRoot(document.StorageRoot);
        if (string.IsNullOrWhiteSpace(document.RelativePath))
        {
            return DrawingFile.Create(document, null, exists: false, storageRoot: root, relativePath: document.RelativePath);
        }

        var fullPath = Path.Combine(root, document.RelativePath);
        var exists = File.Exists(fullPath);
        return DrawingFile.Create(document, fullPath, exists, storageRoot: root, relativePath: document.RelativePath);
    }

    private string ResolveRoot(string? storageRoot)
    {
        var root = string.IsNullOrWhiteSpace(storageRoot) ? _options.RootPath : storageRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = "DrawingStorage";
        }

        return Path.IsPathRooted(root)
            ? root
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), root));
    }
}
