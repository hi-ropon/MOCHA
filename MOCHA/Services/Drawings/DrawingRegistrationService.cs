using System;
using System.Collections.Generic;
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
    private const long _maxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IDrawingRepository _repository;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<DrawingRegistrationService> _logger;

    /// <summary>
    /// 依存リポジトリとロールプロバイダー注入による初期化
    /// </summary>
    /// <param name="repository">図面リポジトリ</param>
    /// <param name="roleProvider">ロールプロバイダー</param>
    /// <param name="logger">ロガー</param>
    public DrawingRegistrationService(
        IDrawingRepository repository,
        IUserRoleProvider roleProvider,
        ILogger<DrawingRegistrationService> logger)
    {
        _repository = repository;
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
    public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(userId, agentNumber, cancellationToken);
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
        if (!await IsAdminAsync(userId, cancellationToken))
        {
            return DrawingRegistrationResult.Fail("管理者のみ図面を登録できます");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return DrawingRegistrationResult.Fail("装置エージェントを選択してください");
        }

        var validation = upload.Validate(_maxFileSizeBytes);
        if (!validation.IsValid)
        {
            return DrawingRegistrationResult.Fail(validation.Error ?? "入力内容が正しくありません");
        }

        var document = DrawingDocument.Create(
            userId,
            agentNumber.Trim(),
            upload.FileName.Trim(),
            upload.ContentType,
            upload.FileSize,
            upload.Description);

        var saved = await _repository.AddAsync(document, cancellationToken);
        _logger.LogInformation("図面を登録しました: {FileName} ({Size} bytes)", saved.FileName, saved.FileSize);
        return DrawingRegistrationResult.Success(saved);
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
        if (!await IsAdminAsync(userId, cancellationToken))
        {
            return DrawingRegistrationResult.Fail("管理者のみ図面を編集できます");
        }

        var existing = await _repository.GetAsync(drawingId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return DrawingRegistrationResult.Fail("図面が見つかりません");
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
    private Task<bool> IsAdminAsync(string userId, CancellationToken cancellationToken)
    {
        return _roleProvider.IsInRoleAsync(userId, UserRoleId.Predefined.Administrator.Value, cancellationToken);
    }
}
