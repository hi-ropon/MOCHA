using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCユニットをDBに永続化するリポジトリ
/// </summary>
internal sealed class PlcUnitRepository : IPlcUnitRepository
{
    private readonly IChatDbContext _dbContext;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// DbContext を受け取り初期化する
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext</param>
    public PlcUnitRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PlcUnit> AddAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        var entity = ToEntity(unit);
        await _dbContext.PlcUnits.AddAsync(entity, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "PlcUnits"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PlcUnits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            _dbContext.PlcUnits.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "PlcUnits"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<PlcUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PlcUnits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            return entity is null ? null : ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "PlcUnits"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Array.Empty<PlcUnit>();
        }

        var normalizedUserId = userId.Trim();
        var normalizedAgent = agentNumber.Trim();

        try
        {
            var list = await _dbContext.PlcUnits
                .Where(x => x.UserId == normalizedUserId && x.AgentNumber == normalizedAgent)
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .Select(ToModel)
                .ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "PlcUnits"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return Array.Empty<PlcUnit>();
        }
    }

    /// <inheritdoc />
    public async Task<PlcUnit> UpdateAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        PlcUnitEntity? entity = null;
        try
        {
            entity = await _dbContext.PlcUnits.FirstOrDefaultAsync(x => x.Id == unit.Id, cancellationToken);
            if (entity is null)
            {
                entity = ToEntity(unit);
                _dbContext.PlcUnits.Add(entity);
            }
            else
            {
                entity.UserId = unit.UserId;
                entity.AgentNumber = unit.AgentNumber;
                entity.Name = unit.Name;
                entity.Manufacturer = unit.Manufacturer;
                entity.Model = unit.Model;
                entity.Role = unit.Role;
                entity.IpAddress = unit.IpAddress;
                entity.Port = unit.Port;
                entity.GatewayHost = unit.GatewayHost;
                entity.GatewayPort = unit.GatewayPort;
                entity.ProgramDescription = unit.ProgramDescription;
                entity.CommentFileJson = SerializeFile(unit.CommentFile);
                entity.ProgramFilesJson = SerializeFiles(unit.ProgramFiles);
                entity.ModulesJson = SerializeModules(unit.Modules);
                entity.FunctionBlocksJson = SerializeFunctionBlocks(unit.FunctionBlocks);
                entity.CreatedAt = unit.CreatedAt;
                entity.UpdatedAt = unit.UpdatedAt;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "PlcUnits"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity!);
        }
    }

    private PlcUnitEntity ToEntity(PlcUnit unit)
    {
        return new PlcUnitEntity
        {
            Id = unit.Id,
            UserId = unit.UserId,
            AgentNumber = unit.AgentNumber,
            Name = unit.Name,
            Manufacturer = unit.Manufacturer,
            Model = unit.Model,
            Role = unit.Role,
            IpAddress = unit.IpAddress,
            Port = unit.Port,
            GatewayHost = unit.GatewayHost,
            GatewayPort = unit.GatewayPort,
            ProgramDescription = unit.ProgramDescription,
            CommentFileJson = SerializeFile(unit.CommentFile),
            ProgramFilesJson = SerializeFiles(unit.ProgramFiles),
            ModulesJson = SerializeModules(unit.Modules),
            FunctionBlocksJson = SerializeFunctionBlocks(unit.FunctionBlocks),
            CreatedAt = unit.CreatedAt,
            UpdatedAt = unit.UpdatedAt
        };
    }

    private PlcUnit ToModel(PlcUnitEntity entity)
    {
        var commentFile = DeserializeFile(entity.CommentFileJson);
        var programFiles = DeserializeFiles(entity.ProgramFilesJson);
        var modules = DeserializeModules(entity.ModulesJson);
        var functionBlocks = DeserializeFunctionBlocks(entity.FunctionBlocksJson);

        return PlcUnit.Restore(
            entity.Id,
            entity.UserId,
            entity.AgentNumber,
            entity.Name,
            entity.Manufacturer ?? string.Empty,
            entity.Model,
            entity.Role,
            entity.IpAddress,
            entity.Port,
            entity.GatewayHost,
            entity.GatewayPort,
            commentFile,
            programFiles,
            entity.ProgramDescription,
            modules,
            functionBlocks,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private string? SerializeFile(PlcFileUpload? file)
    {
        return file is null ? null : JsonSerializer.Serialize(file, _serializerOptions);
    }

    private string SerializeFiles(IReadOnlyCollection<PlcFileUpload> files)
    {
        return JsonSerializer.Serialize(files ?? Array.Empty<PlcFileUpload>(), _serializerOptions);
    }

    private string SerializeModules(IReadOnlyCollection<PlcUnitModule> modules)
    {
        return JsonSerializer.Serialize(modules ?? Array.Empty<PlcUnitModule>(), _serializerOptions);
    }

    private string SerializeFunctionBlocks(IReadOnlyCollection<FunctionBlock> functionBlocks)
    {
        return JsonSerializer.Serialize(functionBlocks ?? Array.Empty<FunctionBlock>(), _serializerOptions);
    }

    private PlcFileUpload? DeserializeFile(string? json)
    {
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PlcFileUpload>(json, _serializerOptions);
    }

    private IReadOnlyCollection<PlcFileUpload> DeserializeFiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PlcFileUpload>();
        }

        var files = JsonSerializer.Deserialize<IReadOnlyCollection<PlcFileUpload>>(json, _serializerOptions);
        return files ?? Array.Empty<PlcFileUpload>();
    }

    private IReadOnlyCollection<PlcUnitModule> DeserializeModules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PlcUnitModule>();
        }

        var modules = JsonSerializer.Deserialize<IReadOnlyCollection<PlcUnitModule>>(json, _serializerOptions);
        return modules ?? Array.Empty<PlcUnitModule>();
    }

    private IReadOnlyCollection<FunctionBlock> DeserializeFunctionBlocks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<FunctionBlock>();
        }

        var blocks = JsonSerializer.Deserialize<IReadOnlyCollection<FunctionBlock>>(json, _serializerOptions);
        return blocks ?? Array.Empty<FunctionBlock>();
    }

    private async Task EnsureTableIfMissingAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS PlcUnits(
                Id TEXT NOT NULL CONSTRAINT PK_PlcUnits PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Name TEXT NOT NULL,
                Manufacturer TEXT NOT NULL,
                Model TEXT NULL,
                Role TEXT NULL,
                IpAddress TEXT NULL,
                Port INTEGER NULL,
                GatewayHost TEXT NULL,
                GatewayPort INTEGER NULL,
                ProgramDescription TEXT NULL,
                CommentFileJson TEXT NULL,
                ProgramFilesJson TEXT NULL,
                ModulesJson TEXT NULL,
                FunctionBlocksJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PlcUnits_UserId_AgentNumber_CreatedAt ON PlcUnits(UserId, AgentNumber, CreatedAt);
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

}
