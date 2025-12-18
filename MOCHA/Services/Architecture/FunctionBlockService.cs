using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Auth;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ファンクションブロック登録・更新を扱うドメインサービス
/// </summary>
public sealed class FunctionBlockService
{
    private const long _maxFileSizeBytes = 10 * 1024 * 1024;
    private readonly IPlcUnitRepository _repository;
    private readonly IPlcFileStoragePathBuilder _pathBuilder;
    private readonly IUserRoleProvider _roleProvider;
    private readonly ILogger<FunctionBlockService> _logger;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    public FunctionBlockService(
        IPlcUnitRepository repository,
        IPlcFileStoragePathBuilder pathBuilder,
        IUserRoleProvider roleProvider,
        ILogger<FunctionBlockService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _pathBuilder = pathBuilder ?? throw new ArgumentNullException(nameof(pathBuilder));
        _roleProvider = roleProvider ?? throw new ArgumentNullException(nameof(roleProvider));
        _logger = logger;
    }

    /// <summary>
    /// ファンクションブロックを追加
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="plcUnitId">PLCユニットID</param>
    /// <param name="draft">登録内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果</returns>
    public async Task<FunctionBlockResult> AddAsync(
        string userId,
        string agentNumber,
        Guid plcUnitId,
        FunctionBlockDraft draft,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, agentNumber, draft);
        if (!validation.IsValid)
        {
            return FunctionBlockResult.Fail(validation.Error!);
        }

        var unit = await _repository.GetAsync(plcUnitId, cancellationToken);
        if (unit is null)
        {
            return FunctionBlockResult.Fail("PLCユニットが見つかりません");
        }

        if (!string.Equals(unit.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return FunctionBlockResult.Fail("別のユーザーまたはエージェントに紐づいています");
        }

        var isOwner = string.Equals(unit.UserId, userId, StringComparison.Ordinal);
        if (!isOwner && !await HasAdminOrDeveloperRoleAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return FunctionBlockResult.Fail("管理者または開発者のみ編集できます");
        }

        if (unit.FunctionBlocks.Any(f => string.Equals(f.Name, draft.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return FunctionBlockResult.Fail("同名のファンクションブロックが既に存在します");
        }

        var safeName = SanitizeName(draft.Name);
        var labelFile = await SaveFileAsync(agentNumber, safeName, "Label", draft.LabelFile!, cancellationToken);
        if (labelFile is null)
        {
            return FunctionBlockResult.Fail("ラベルファイルの保存に失敗しました");
        }

        var programFile = await SaveFileAsync(agentNumber, safeName, "Program", draft.ProgramFile!, cancellationToken);
        if (programFile is null)
        {
            return FunctionBlockResult.Fail("プログラムファイルの保存に失敗しました");
        }

        var fb = FunctionBlock.Create(draft.Name.Trim(), safeName, labelFile, programFile);
        var updatedBlocks = unit.FunctionBlocks.Concat(new[] { fb }).ToList();
        var updated = unit.WithFunctionBlocks(updatedBlocks);
        var persisted = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("ファンクションブロックを追加しました: {Name}", fb.Name);
        return FunctionBlockResult.Success(persisted.FunctionBlocks.Single(x => x.Id == fb.Id));
    }

    /// <summary>
    /// ファンクションブロック一覧取得
    /// </summary>
    public async Task<IReadOnlyCollection<FunctionBlock>> ListAsync(string userId, string agentNumber, Guid plcUnitId, CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetAsync(plcUnitId, cancellationToken);
        if (unit is null)
        {
            return Array.Empty<FunctionBlock>();
        }

        if (!string.Equals(unit.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return Array.Empty<FunctionBlock>();
        }

        if (!string.Equals(unit.UserId, userId, StringComparison.Ordinal) &&
            !await HasAdminOrDeveloperRoleAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<FunctionBlock>();
        }

        return unit.FunctionBlocks.ToList();
    }

    /// <summary>
    /// ファンクションブロック取得
    /// </summary>
    public async Task<FunctionBlock?> GetAsync(string userId, string agentNumber, Guid plcUnitId, Guid functionBlockId, CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(userId, agentNumber, plcUnitId, cancellationToken);
        return list.FirstOrDefault(f => f.Id == functionBlockId);
    }

    /// <summary>
    /// ファイル内容を取得
    /// </summary>
    public (string? Label, string? Program) ReadContents(FunctionBlock block)
    {
        var labelContent = ReadFile(block.LabelFile);
        var programContent = ReadFile(block.ProgramFile);
        return (labelContent, programContent);
    }

    /// <summary>
    /// ファンクションブロック削除
    /// </summary>
    public async Task<bool> DeleteAsync(string userId, string agentNumber, Guid plcUnitId, Guid functionBlockId, CancellationToken cancellationToken = default)
    {
        var unit = await _repository.GetAsync(plcUnitId, cancellationToken);
        if (unit is null ||
            !string.Equals(unit.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(unit.UserId, userId, StringComparison.Ordinal) &&
            !await HasAdminOrDeveloperRoleAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var target = unit.FunctionBlocks.FirstOrDefault(f => f.Id == functionBlockId);
        if (target is null)
        {
            return false;
        }

        DeletePhysical(target.LabelFile);
        DeletePhysical(target.ProgramFile);

        var remaining = unit.FunctionBlocks.Where(f => f.Id != functionBlockId).ToList();
        var updated = unit.WithFunctionBlocks(remaining);
        await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("ファンクションブロックを削除しました: {Name}", target.Name);
        return true;
    }

    private async Task<bool> HasAdminOrDeveloperRoleAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

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

    private (bool IsValid, string? Error) Validate(string userId, string agentNumber, FunctionBlockDraft draft)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, "ユーザーIDが空です");
        }

        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return (false, "装置エージェントを選択してください");
        }

        return draft.Validate(_maxFileSizeBytes);
    }

    private async Task<PlcFileUpload?> SaveFileAsync(
        string agentNumber,
        string safeName,
        string suffix,
        PlcFileUpload source,
        CancellationToken cancellationToken)
    {
        var fileName = $"{safeName}_{suffix}.csv";
        var storagePath = _pathBuilder.Build(agentNumber, fileName, "function_blocks");
        try
        {
            Directory.CreateDirectory(storagePath.DirectoryPath);
            await File.WriteAllBytesAsync(storagePath.FullPath, source.Content!, cancellationToken);

            return new PlcFileUpload
            {
                Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
                FileName = fileName,
                ContentType = source.ContentType,
                FileSize = source.Content!.LongLength,
                DisplayName = string.IsNullOrWhiteSpace(source.DisplayName) ? fileName : source.DisplayName,
                RelativePath = storagePath.RelativePath,
                StorageRoot = storagePath.RootPath,
                Content = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ファイル保存に失敗しました: {File}", fileName);
            return null;
        }
    }

    private static string SanitizeName(string name)
    {
        var trimmed = name.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        var result = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "function_block" : result;
    }

    private string? ReadFile(PlcFileUpload file)
    {
        var path = Combine(file);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        foreach (var encoding in new[] { "utf-8", "shift_jis", "cp932", "utf-16", "utf-16le", "utf-16be" })
        {
            try
            {
                var content = File.ReadAllText(path, Encoding.GetEncoding(encoding));
                if (content.Contains('\0'))
                {
                    continue;
                }

                return content;
            }
            catch (DecoderFallbackException)
            {
                continue;
            }
            catch (ArgumentException)
            {
                continue;
            }
        }

        try
        {
            return Encoding.UTF8.GetString(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ファイル読取に失敗しました: {Path}", path);
            return null;
        }
    }

    private static string? Combine(PlcFileUpload file)
    {
        if (string.IsNullOrWhiteSpace(file.StorageRoot) || string.IsNullOrWhiteSpace(file.RelativePath))
        {
            return null;
        }

        return Path.Combine(file.StorageRoot, file.RelativePath);
    }

    private void DeletePhysical(PlcFileUpload file)
    {
        var path = Combine(file);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ファイル削除に失敗しました: {Path}", path);
        }
    }
}
