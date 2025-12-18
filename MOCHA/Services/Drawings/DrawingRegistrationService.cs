using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Auth;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面登録と更新の業務ロジック
/// </summary>
internal sealed class DrawingRegistrationService
{
    private const long _maxFileSizeBytes = 20 * 1024 * 1024;

    private readonly IDrawingRepository _repository;
    private readonly IDrawingStoragePathBuilder _pathBuilder;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<DrawingRegistrationService> _logger;

    /// <summary>
    /// 依存リポジトリとロールプロバイダー注入による初期化
    /// </summary>
    /// <param name="repository">図面リポジトリ</param>
    /// <param name="pathBuilder">保存パスビルダー</param>
    /// <param name="roleProvider">ロールプロバイダー</param>
    /// <param name="logger">ロガー</param>
    public DrawingRegistrationService(
        IDrawingRepository repository,
        IDrawingStoragePathBuilder pathBuilder,
        IUserRoleProvider roleProvider,
        ILogger<DrawingRegistrationService> logger)
    {
        _repository = repository;
        _pathBuilder = pathBuilder;
        _roleProvider = roleProvider;
        _logger = logger;
    }

    /// <summary>
    /// 図面一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面一覧</returns>
    public Task<IReadOnlyList<DrawingDocument>> ListAsync(string? agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<DrawingDocument>>(Array.Empty<DrawingDocument>());
        }

        return _repository.ListAsync(agentNumber, cancellationToken);
    }

    /// <summary>
    /// 図面登録
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="upload">アップロード情報</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>登録結果</returns>
    public async Task<DrawingRegistrationResult> RegisterAsync(
        string userId,
        string? agentNumber,
        DrawingUpload upload,
        CancellationToken cancellationToken = default)
    {
        var batchResult = await RegisterManyAsync(userId, agentNumber, new[] { upload }, cancellationToken);
        if (!batchResult.Succeeded)
        {
            return DrawingRegistrationResult.Fail(batchResult.Error ?? "入力内容が正しくありません");
        }

        var first = batchResult.Documents?.Count > 0 ? batchResult.Documents[0] : null;
        if (first is null)
        {
            return DrawingRegistrationResult.Fail("入力内容が正しくありません");
        }

        _logger.LogInformation("図面を登録しました: {FileName} ({Size} bytes)", first.FileName, first.FileSize);
        return DrawingRegistrationResult.Success(first);
    }

    /// <summary>
    /// 図面複数登録
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="uploads">アップロード情報一覧</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>登録結果</returns>
    public async Task<DrawingBatchRegistrationResult> RegisterManyAsync(
        string userId,
        string? agentNumber,
        IReadOnlyCollection<DrawingUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return DrawingBatchRegistrationResult.Fail("管理者または開発者のみ図面を登録できます");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return DrawingBatchRegistrationResult.Fail("装置エージェントを選択してください");
        }

        if (uploads is null || uploads.Count == 0)
        {
            return DrawingBatchRegistrationResult.Fail("図面ファイルを選択してください");
        }

        var agent = agentNumber.Trim();
        var documents = new List<DrawingDocument>();
        foreach (var upload in uploads)
        {
            var validation = upload.Validate(_maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return DrawingBatchRegistrationResult.Fail(validation.Error ?? "入力内容が正しくありません");
            }

            var storagePath = _pathBuilder.Build(agent, upload.FileName.Trim());
            try
            {
                Directory.CreateDirectory(storagePath.DirectoryPath);
                await File.WriteAllBytesAsync(storagePath.FullPath, upload.Content!, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "図面ファイルの保存に失敗しました: {FileName}", upload.FileName);
                return DrawingBatchRegistrationResult.Fail("図面ファイルの保存に失敗しました");
            }

            documents.Add(DrawingDocument.Create(
                userId,
                agent,
                upload.FileName.Trim(),
                upload.ContentType,
                upload.Content!.LongLength,
                upload.Description,
                createdAt: null,
                relativePath: storagePath.RelativePath,
                storageRoot: storagePath.RootPath));
        }

        var savedDocuments = new List<DrawingDocument>();
        foreach (var document in documents)
        {
            var saved = await _repository.AddAsync(document, cancellationToken);
            savedDocuments.Add(saved);
        }

        _logger.LogInformation("図面を登録しました: {Count} 件", savedDocuments.Count);
        return DrawingBatchRegistrationResult.Success(savedDocuments);
    }

    /// <summary>
    /// 図面説明更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="drawingId">図面ID</param>
    /// <param name="description">説明</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新結果</returns>
    public async Task<DrawingRegistrationResult> UpdateDescriptionAsync(
        string userId,
        Guid drawingId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return DrawingRegistrationResult.Fail("管理者または開発者のみ図面を編集できます");
        }

        var existing = await _repository.GetAsync(drawingId, cancellationToken);
        if (existing is null)
        {
            return DrawingRegistrationResult.Fail("図面が見つかりません");
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) &&
            !await HasEditPermissionAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return DrawingRegistrationResult.Fail("管理者または開発者のみ編集できます");
        }

        var updated = existing.WithDescription(description);
        updated = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("図面の説明を更新しました: {DrawingId}", drawingId);
        return DrawingRegistrationResult.Success(updated);
    }

    /// <summary>
    /// 管理者ロール保持判定
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>管理者なら true</returns>
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

    /// <summary>
    /// 図面削除
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="drawingId">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除結果</returns>
    public async Task<DrawingDeletionResult> DeleteAsync(string userId, Guid drawingId, CancellationToken cancellationToken = default)
    {
        if (!await HasEditPermissionAsync(userId, cancellationToken))
        {
            return DrawingDeletionResult.Fail("管理者または開発者のみ図面を削除できます");
        }

        var existing = await _repository.GetAsync(drawingId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return DrawingDeletionResult.Fail("図面が見つかりません");
        }

        var deleted = await _repository.DeleteAsync(drawingId, cancellationToken);
        if (!deleted)
        {
            return DrawingDeletionResult.Fail("図面削除に失敗しました");
        }

        if (!string.IsNullOrWhiteSpace(existing.StorageRoot) && !string.IsNullOrWhiteSpace(existing.RelativePath))
        {
            var root = ResolveRoot(existing.StorageRoot!);
            var fullPath = Path.Combine(root, existing.RelativePath!);
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "図面ファイルの削除に失敗しました: {Path}", fullPath);
            }
        }

        _logger.LogInformation("図面を削除しました: {DrawingId}", drawingId);
        return DrawingDeletionResult.Success();
    }

    private static string ResolveRoot(string rootPath)
    {
        if (Path.IsPathRooted(rootPath))
        {
            return rootPath;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rootPath));
    }
}
