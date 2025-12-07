using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MOCHA.Data;

namespace MOCHA.Factories;

/// <summary>
/// SQLite 環境で必要なテーブルやインデックスを作成する初期化クラス
/// </summary>
internal sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly ChatDbContext _db;

    /// <summary>
    /// DbContext 受け取りによる初期化
    /// </summary>
    /// <param name="db">チャット用 DbContext</param>
    public SqliteDatabaseInitializer(ChatDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// テーブル作成と列定義確認
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTablesAsync(cancellationToken);
    }

    /// <summary>
    /// 会話・メッセージ・ロール・エージェントのテーブル作成処理
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS Conversations(
                Id TEXT NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
                UserObjectId TEXT NOT NULL,
                Title TEXT NOT NULL,
                AgentNumber TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Conversations_UserObjectId_UpdatedAt ON Conversations(UserObjectId, UpdatedAt);
            CREATE INDEX IF NOT EXISTS IX_Conversations_UserObjectId_AgentNumber_UpdatedAt ON Conversations(UserObjectId, AgentNumber, UpdatedAt);

            CREATE TABLE IF NOT EXISTS Messages(
                Id INTEGER NOT NULL CONSTRAINT PK_Messages PRIMARY KEY AUTOINCREMENT,
                ConversationId TEXT NOT NULL,
                UserObjectId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_Messages_Conversations_ConversationId FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_Messages_ConversationId ON Messages(ConversationId);
            CREATE INDEX IF NOT EXISTS IX_Messages_UserObjectId_CreatedAt ON Messages(UserObjectId, CreatedAt);

            CREATE TABLE IF NOT EXISTS Attachments(
                Id TEXT NOT NULL CONSTRAINT PK_Attachments PRIMARY KEY,
                MessageId INTEGER NOT NULL,
                ConversationId TEXT NOT NULL,
                UserObjectId TEXT NOT NULL,
                FileName TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                Size INTEGER NOT NULL,
                ThumbSmallBase64 TEXT NOT NULL,
                ThumbMediumBase64 TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_Attachments_Messages_MessageId FOREIGN KEY (MessageId) REFERENCES Messages (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_Attachments_MessageId ON Attachments(MessageId);

            CREATE TABLE IF NOT EXISTS UserRoles(
                Id INTEGER NOT NULL CONSTRAINT PK_UserRoles PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Role TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_UserRoles_UserId_Role ON UserRoles(UserId, Role);

            CREATE TABLE IF NOT EXISTS DeviceAgents(
                Id INTEGER NOT NULL CONSTRAINT PK_DeviceAgents PRIMARY KEY AUTOINCREMENT,
                UserObjectId TEXT NOT NULL,
                Number TEXT NOT NULL,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceAgents_UserObjectId_Number ON DeviceAgents(UserObjectId, Number);

            CREATE TABLE IF NOT EXISTS DeviceAgentPermissions(
                Id INTEGER NOT NULL CONSTRAINT PK_DeviceAgentPermissions PRIMARY KEY AUTOINCREMENT,
                UserObjectId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceAgentPermissions_UserObjectId_AgentNumber ON DeviceAgentPermissions(UserObjectId, AgentNumber);

            CREATE TABLE IF NOT EXISTS DevUsers(
                Id INTEGER NOT NULL CONSTRAINT PK_DevUsers PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DevUsers_Email ON DevUsers(Email);

            CREATE TABLE IF NOT EXISTS Feedbacks(
                Id INTEGER NOT NULL CONSTRAINT PK_Feedbacks PRIMARY KEY AUTOINCREMENT,
                ConversationId TEXT NOT NULL,
                MessageIndex INTEGER NOT NULL,
                UserObjectId TEXT NOT NULL,
                Rating TEXT NOT NULL,
                Comment TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Feedbacks_ConversationId_MessageIndex_UserObjectId ON Feedbacks(ConversationId, MessageIndex, UserObjectId);
            CREATE INDEX IF NOT EXISTS IX_Feedbacks_UserObjectId_CreatedAt ON Feedbacks(UserObjectId, CreatedAt);

            CREATE TABLE IF NOT EXISTS Drawings(
                Id TEXT NOT NULL CONSTRAINT PK_Drawings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NULL,
                FileName TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                Description TEXT NULL,
                RelativePath TEXT NULL,
                StorageRoot TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Drawings_UserId_AgentNumber_CreatedAt ON Drawings(UserId, AgentNumber, CreatedAt);

            CREATE TABLE IF NOT EXISTS PcSettings(
                Id TEXT NOT NULL CONSTRAINT PK_PcSettings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Os TEXT NOT NULL,
                Role TEXT NULL,
                RepositoryUrlsJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PcSettings_UserId_AgentNumber_CreatedAt ON PcSettings(UserId, AgentNumber, CreatedAt);

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
                CommentFileJson TEXT NULL,
                ProgramFilesJson TEXT NULL,
                ModulesJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PlcUnits_UserId_AgentNumber_CreatedAt ON PlcUnits(UserId, AgentNumber, CreatedAt);

            CREATE TABLE IF NOT EXISTS GatewaySettings(
                Id TEXT NOT NULL CONSTRAINT PK_GatewaySettings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Host TEXT NOT NULL,
                Port INTEGER NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_GatewaySettings_UserId_AgentNumber ON GatewaySettings(UserId, AgentNumber);
        """;

        await _db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await EnsureAgentColumnAsync(cancellationToken);
        await EnsurePlcManufacturerColumnAsync(cancellationToken);
        await EnsurePlcGatewayColumnsAsync(cancellationToken);
    }

    /// <summary>
    /// Conversations テーブルへの AgentNumber 列追加確認
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsureAgentColumnAsync(CancellationToken cancellationToken)
    {
        const string pragmaSql = "PRAGMA table_info(Conversations);";
        var hasAgentColumn = false;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var command = new SqliteCommand(pragmaSql, (SqliteConnection)connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "AgentNumber", StringComparison.OrdinalIgnoreCase))
                {
                    hasAgentColumn = true;
                    break;
                }
            }
        }

        if (shouldClose)
        {
            await connection.CloseAsync();
        }

        if (hasAgentColumn)
        {
            return;
        }

        const string alterSql = """
            ALTER TABLE Conversations ADD COLUMN AgentNumber TEXT NULL;
            CREATE INDEX IF NOT EXISTS IX_Conversations_UserObjectId_AgentNumber_UpdatedAt ON Conversations(UserObjectId, AgentNumber, UpdatedAt);
        """;
        await _db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
    }

    /// <summary>
    /// PlcUnits テーブルへの Manufacturer 列追加確認
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsurePlcManufacturerColumnAsync(CancellationToken cancellationToken)
    {
        const string pragmaSql = "PRAGMA table_info(PlcUnits);";
        var hasColumn = false;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var command = new SqliteCommand(pragmaSql, (SqliteConnection)connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "Manufacturer", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (shouldClose)
        {
            await connection.CloseAsync();
        }

        if (hasColumn)
        {
            return;
        }

        const string alterSql = "ALTER TABLE PlcUnits ADD COLUMN Manufacturer TEXT NULL;";
        await _db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
    }

    /// <summary>
    /// PlcUnits テーブルへのゲートウェイ列追加確認
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsurePlcGatewayColumnsAsync(CancellationToken cancellationToken)
    {
        const string pragmaSql = "PRAGMA table_info(PlcUnits);";
        var hasGatewayHost = false;
        var hasGatewayPort = false;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var command = new SqliteCommand(pragmaSql, (SqliteConnection)connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "GatewayHost", StringComparison.OrdinalIgnoreCase))
                {
                    hasGatewayHost = true;
                }

                if (string.Equals(name, "GatewayPort", StringComparison.OrdinalIgnoreCase))
                {
                    hasGatewayPort = true;
                }

                if (hasGatewayHost && hasGatewayPort)
                {
                    break;
                }
            }
        }

        if (shouldClose)
        {
            await connection.CloseAsync();
        }

        var commands = new List<string>();
        if (!hasGatewayHost)
        {
            commands.Add("ALTER TABLE PlcUnits ADD COLUMN GatewayHost TEXT NULL;");
        }

        if (!hasGatewayPort)
        {
            commands.Add("ALTER TABLE PlcUnits ADD COLUMN GatewayPort INTEGER NULL;");
        }

        if (commands.Count == 0)
        {
            return;
        }

        var alterSql = string.Join(Environment.NewLine, commands);
        await _db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
    }
}
