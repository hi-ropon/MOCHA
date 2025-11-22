using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MOCHA.Data;

namespace MOCHA.Factories;

/// <summary>
/// SQLite 環境で必要なテーブルやインデックスを作成する初期化クラス。
/// </summary>
public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly ChatDbContext _db;

    /// <summary>
    /// DbContext を受け取り初期化する。
    /// </summary>
    /// <param name="db">チャット用 DbContext。</param>
    public SqliteDatabaseInitializer(ChatDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// テーブルを作成し、必要な列が揃っているか確認する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTablesAsync(cancellationToken);
    }

    /// <summary>
    /// 会話・メッセージ・ロール・エージェントのテーブルを作成する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
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
        """;

        await _db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await EnsureAgentColumnAsync(cancellationToken);
    }

    /// <summary>
    /// Conversations テーブルに AgentNumber 列が無い場合に追加する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    private async Task EnsureAgentColumnAsync(CancellationToken cancellationToken)
    {
        const string pragmaSql = "PRAGMA table_info(Conversations);";
        var hasAgentColumn = false;

        await using (var connection = new SqliteConnection(_db.Database.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqliteCommand(pragmaSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        if (!hasAgentColumn)
        {
            const string alterSql = """
                ALTER TABLE Conversations ADD COLUMN AgentNumber TEXT NULL;
                CREATE INDEX IF NOT EXISTS IX_Conversations_UserObjectId_AgentNumber_UpdatedAt ON Conversations(UserObjectId, AgentNumber, UpdatedAt);
            """;
            await _db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
        }
    }
}
