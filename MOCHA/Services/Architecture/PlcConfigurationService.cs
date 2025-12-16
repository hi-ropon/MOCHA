using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCユニット設定を管理するドメインサービス
/// </summary>
internal sealed class PlcConfigurationService
{
    private const long _maxFileSizeBytesm = 10 * 1024 * 1024;
    private readonly IPlcUnitRepository _repository;
    private readonly IPlcFileStoragePathBuilder _pathBuilder;
    private readonly ILogger<PlcConfigurationService> _logger;

    /// <summary>
    /// リポジトリとロガー注入による初期化
    /// </summary>
    /// <param name="repository">PLCユニットリポジトリ</param>
    /// <param name="logger">ロガー</param>
    /// <param name="pathBuilder">ファイル保存パスビルダー</param>
    public PlcConfigurationService(
        IPlcUnitRepository repository,
        IPlcFileStoragePathBuilder pathBuilder,
        ILogger<PlcConfigurationService> logger)
    {
        _repository = repository;
        _pathBuilder = pathBuilder;
        _logger = logger;
    }

    /// <summary>
    /// エージェント単位のユニット一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    public Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<PlcUnit>>(Array.Empty<PlcUnit>());
        }

        var normalizedAgent = agentNumber.Trim();
        return _repository.ListAsync(normalizedAgent, cancellationToken);
    }

    /// <summary>
    /// PLCユニット追加
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">登録内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PlcUnitResult> AddAsync(string userId, string agentNumber, PlcUnitDraft draft, CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PlcUnitResult.Fail(validation.Error!);
        }

        var processed = await SaveFilesAsync(agentNumber.Trim(), draft, existing: null, cancellationToken);
        if (!processed.Succeeded)
        {
            return PlcUnitResult.Fail(processed.Error!);
        }

        var unit = PlcUnit.Create(userId.Trim(), agentNumber.Trim(), processed.Draft);
        var saved = await _repository.AddAsync(unit, cancellationToken);
        _logger.LogInformation("PLCユニットを登録しました: {Name}", saved.Name);
        return PlcUnitResult.Success(saved);
    }

    /// <summary>
    /// PLCユニット更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="draft">更新内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<PlcUnitResult> UpdateAsync(string userId, string agentNumber, Guid unitId, PlcUnitDraft draft, CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return PlcUnitResult.Fail(validation.Error!);
        }

        var existing = await _repository.GetAsync(unitId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return PlcUnitResult.Fail("PLCユニットが見つかりません");
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return PlcUnitResult.Fail("別の装置エージェントに紐づくため更新できません");
        }

        var processed = await SaveFilesAsync(agentNumber.Trim(), draft, existing, cancellationToken);
        if (!processed.Succeeded)
        {
            return PlcUnitResult.Fail(processed.Error!);
        }

        var updated = existing.Update(processed.Draft);
        updated = await _repository.UpdateAsync(updated, cancellationToken);

        foreach (var oldPath in processed.PathsToDelete)
        {
            DeletePhysicalFile(oldPath);
        }

        _logger.LogInformation("PLCユニットを更新しました: {Name}", updated.Name);
        return PlcUnitResult.Success(updated);
    }

    /// <summary>
    /// PLCユニット削除
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="unitId">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    public async Task<bool> DeleteAsync(string userId, string agentNumber, Guid unitId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(unitId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) ||
            !string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return false;
        }

        var deleted = await _repository.DeleteAsync(unitId, cancellationToken);
        if (deleted)
        {
            DeletePhysicalFile(existing.CommentFile);
            foreach (var file in existing.ProgramFiles ?? Array.Empty<PlcFileUpload>())
            {
                DeletePhysicalFile(file);
            }
            _logger.LogInformation("PLCユニットを削除しました: {Id}", unitId);
        }

        return deleted;
    }

    /// <summary>
    /// 入力値とドラフトのバリデーション
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">ユニットドラフト</param>
    /// <returns>検証結果</returns>
    private (bool IsValid, string? Error) Validate(string userId, string agentNumber, PlcUnitDraft draft)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, "ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return (false, "装置エージェントを選択してください");
        }

        var validation = draft.Validate(_maxFileSizeBytesm);
        if (!validation.IsValid)
        {
            return validation;
        }

        return (true, null);
    }

    private async Task<(bool Succeeded, string? Error, PlcUnitDraft Draft, List<string> PathsToDelete)> SaveFilesAsync(
        string agentNumber,
        PlcUnitDraft draft,
        PlcUnit? existing,
        CancellationToken cancellationToken)
    {
        var updatedComment = existing?.CommentFile;
        var pathsToDelete = new List<string>();

        if (draft.CommentFile is not null)
        {
            var commentContent = draft.CommentFile.Content;
            if (commentContent is null || commentContent.Length == 0)
            {
                if (existing?.CommentFile is null)
                {
                    return (false, "コメントファイルの内容がありません", draft, pathsToDelete);
                }
            }
            else
            {
                var commentPath = _pathBuilder.Build(agentNumber, draft.CommentFile.FileName, "comment");
                if (!TryEnsureDirectory(commentPath.DirectoryPath, out var err))
                {
                    return (false, err, draft, pathsToDelete);
                }

                await File.WriteAllBytesAsync(commentPath.FullPath, commentContent, cancellationToken);
                if (existing?.CommentFile is not null)
                {
                    var oldPath = Combine(existing.CommentFile);
                    if (!string.IsNullOrWhiteSpace(oldPath))
                    {
                        pathsToDelete.Add(oldPath!);
                    }
                }

                updatedComment = CloneFile(draft.CommentFile, commentContent.LongLength, commentPath.RelativePath, commentPath.RootPath);
            }
        }

        var processedPrograms = new List<PlcFileUpload>();
        var existingPrograms = existing?.ProgramFiles?.ToDictionary(f => f.Id, f => f) ?? new Dictionary<Guid, PlcFileUpload>();

        foreach (var incoming in draft.ProgramFiles ?? Array.Empty<PlcFileUpload>())
        {
            var hasContent = incoming.Content is not null && incoming.Content.Length > 0;
            if (!existingPrograms.TryGetValue(incoming.Id, out var current))
            {
                if (!hasContent)
                {
                    return (false, "プログラムファイルの内容がありません", draft, pathsToDelete);
                }

                var path = _pathBuilder.Build(agentNumber, incoming.FileName, "program");
                if (!TryEnsureDirectory(path.DirectoryPath, out var err))
                {
                    return (false, err, draft, pathsToDelete);
                }

                await File.WriteAllBytesAsync(path.FullPath, incoming.Content!, cancellationToken);
                processedPrograms.Add(CloneFile(incoming, incoming.Content!.LongLength, path.RelativePath, path.RootPath));
            }
            else if (hasContent)
            {
                var path = _pathBuilder.Build(agentNumber, incoming.FileName, "program");
                if (!TryEnsureDirectory(path.DirectoryPath, out var err))
                {
                    return (false, err, draft, pathsToDelete);
                }

                await File.WriteAllBytesAsync(path.FullPath, incoming.Content!, cancellationToken);
                var oldPath = Combine(current);
                if (!string.IsNullOrWhiteSpace(oldPath))
                {
                    pathsToDelete.Add(oldPath!);
                }

                processedPrograms.Add(CloneFile(incoming, incoming.Content!.LongLength, path.RelativePath, path.RootPath));
            }
            else
            {
                processedPrograms.Add(current);
            }
        }

        if (existingPrograms.Count > 0)
        {
            var incomingIds = new HashSet<Guid>((draft.ProgramFiles ?? Array.Empty<PlcFileUpload>()).Select(f => f.Id));
            foreach (var kv in existingPrograms)
            {
                if (!incomingIds.Contains(kv.Key))
                {
                    var oldPath = Combine(kv.Value);
                    if (!string.IsNullOrWhiteSpace(oldPath))
                    {
                        pathsToDelete.Add(oldPath!);
                    }
                }
            }
        }

        var updatedDraft = new PlcUnitDraft
        {
            Name = draft.Name,
            Manufacturer = draft.Manufacturer,
            Model = draft.Model,
            Role = draft.Role,
            IpAddress = draft.IpAddress,
            Port = draft.Port,
            Transport = draft.Transport,
            GatewayHost = draft.GatewayHost,
            GatewayPort = draft.GatewayPort,
            ProgramDescription = draft.ProgramDescription,
            Modules = draft.Modules,
            CommentFile = updatedComment,
            ProgramFiles = processedPrograms
        };

        return (true, null, updatedDraft, pathsToDelete);
    }

    private static bool TryEnsureDirectory(string directoryPath, out string? error)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"ファイル保存用ディレクトリ作成に失敗しました: {ex.Message}";
            return false;
        }
    }

    private static string? Combine(PlcFileUpload? file)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.StorageRoot) || string.IsNullOrWhiteSpace(file.RelativePath))
        {
            return null;
        }

        return Path.Combine(file.StorageRoot, file.RelativePath);
    }

    private void DeletePhysicalFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ファイル削除に失敗しました: {Path}", fullPath);
        }
    }

    private void DeletePhysicalFile(PlcFileUpload? file)
    {
        var path = Combine(file);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DeletePhysicalFile(path);
        }
    }

    private static PlcFileUpload CloneFile(PlcFileUpload source, long size, string? relativePath, string? storageRoot)
    {
        return new PlcFileUpload
        {
            Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
            FileName = source.FileName,
            ContentType = source.ContentType,
            FileSize = size,
            DisplayName = source.DisplayName,
            RelativePath = relativePath,
            StorageRoot = storageRoot,
            Content = null
        };
    }
}
