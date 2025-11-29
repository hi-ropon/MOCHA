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
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IDrawingRepository _repository;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<DrawingRegistrationService> _logger;

    public DrawingRegistrationService(
        IDrawingRepository repository,
        IUserRoleProvider roleProvider,
        ILogger<DrawingRegistrationService> logger)
    {
        _repository = repository;
        _roleProvider = roleProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(userId, agentNumber, cancellationToken);
    }

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

        var validation = upload.Validate(MaxFileSizeBytes);
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

    private Task<bool> IsAdminAsync(string userId, CancellationToken cancellationToken)
    {
        return _roleProvider.IsInRoleAsync(userId, UserRoleId.Predefined.Administrator.Value, cancellationToken);
    }
}
