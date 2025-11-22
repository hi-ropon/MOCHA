using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MOCHA.Data;

namespace MOCHA.Factories;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly ChatDbContext _db;

    public SqliteDatabaseInitializer(ChatDbContext db)
    {
        _db = db;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // マイグレーションを適用。初回はDB/テーブルを作成し、以降は差分を反映。
            await _db.Database.MigrateAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // 旧DBで __EFMigrationsHistory が無い/壊れている場合のフォールバックとして、最低限のテーブルを確保する。
            await EnsureTablesAsync(cancellationToken);
            return;
        }

        // マイグレーションが通っても、既存DBにテーブルが無い場合のフォールバック
        if (!await HasConversationTableAsync(cancellationToken))
        {
            await EnsureTablesAsync(cancellationToken);
        }
    }

    private async Task<bool> HasConversationTableAsync(CancellationToken cancellationToken)
    {
        const string existsSql = """
            SELECT name FROM sqlite_master WHERE type='table' AND name='Conversations';
        """;

        await using var connection = new SqliteConnection(_db.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(existsSql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS Conversations(
                Id TEXT NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
                UserObjectId TEXT NOT NULL,
                Title TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Conversations_UserObjectId_UpdatedAt ON Conversations(UserObjectId, UpdatedAt);

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
        """;

        await _db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }
}
