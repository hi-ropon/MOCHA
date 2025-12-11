using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;

namespace MOCHA.Factories;

/// <summary>
/// PostgreSQL で EF Core マイグレーションを適用する初期化処理
/// </summary>
internal sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private readonly ChatDbContext _dbContext;

    /// <summary>
    /// DbContext 注入による初期化
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext</param>
    public PostgresDatabaseInitializer(ChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
