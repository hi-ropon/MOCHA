using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Factories;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// PlcUnitRepository のDB永続化テスト
/// </summary>
[TestClass]
public class PlcUnitRepositoryTests
{
    /// <summary>
    /// ファイルとモジュールを含めて保存できることを確認
    /// </summary>
    [TestMethod]
    public async Task ファイルとモジュールを保存できる()
    {
        await using var harness = await CreateRepositoryAsync();
        var unit = PlcUnit.Create(
            "user-1",
            "A-01",
            new PlcUnitDraft
            {
                Name = "PLC-1",
                Manufacturer = "三菱電機",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                CommentFile = new PlcFileUpload { FileName = "comment.csv", FileSize = 1024 },
                ProgramFiles = new[]
                {
                    new PlcFileUpload { FileName = "prog1.csv", FileSize = 2048, DisplayName = "メイン" },
                    new PlcFileUpload { FileName = "prog2.csv", FileSize = 4096 }
                },
                Modules = new[]
                {
                    new PlcModuleDraft { Name = "入力", Specification = "16点" }
                }
            });

        await harness.Repository.AddAsync(unit);

        var list = await harness.Repository.ListAsync("A-01");

        Assert.AreEqual(1, list.Count);
        var loaded = list[0];
        Assert.AreEqual("PLC-1", loaded.Name);
        Assert.AreEqual("comment.csv", loaded.CommentFile!.FileName);
        Assert.AreEqual(2, loaded.ProgramFiles.Count);
        Assert.AreEqual("入力", loaded.Modules.First().Name);
    }

    /// <summary>
    /// 更新内容が保存されることを確認
    /// </summary>
    [TestMethod]
    public async Task 更新後の内容が反映される()
    {
        await using var harness = await CreateRepositoryAsync();
        var original = PlcUnit.Create(
            "user-2",
            "B-02",
            new PlcUnitDraft
            {
                Name = "PLC-2",
                Manufacturer = "KEYENCE",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                CommentFile = new PlcFileUpload { FileName = "comment.csv", FileSize = 512 }
            });
        var saved = await harness.Repository.AddAsync(original);

        var updatedDraft = new PlcUnitDraft
        {
            Name = "PLC-2B",
            Manufacturer = "三菱電機",
            IpAddress = "192.168.0.10",
            Port = 5000,
            GatewayHost = "192.168.0.20",
            GatewayPort = 9000,
            ProgramFiles = new[]
            {
                new PlcFileUpload { FileName = "prog.csv", FileSize = 1024 }
            },
            Modules = new[]
            {
                new PlcModuleDraft { Name = "出力", Specification = "8点" }
            }
        };

        var updated = await harness.Repository.UpdateAsync(saved.Update(updatedDraft));

        Assert.AreEqual("PLC-2B", updated.Name);
        Assert.AreEqual("192.168.0.10", updated.IpAddress);
        Assert.AreEqual(5000, updated.Port);
        Assert.AreEqual(1, updated.ProgramFiles.Count);
        Assert.AreEqual("出力", updated.Modules.First().Name);
    }

    /// <summary>
    /// 削除で対象が取得できなくなることを確認
    /// </summary>
    [TestMethod]
    public async Task 削除すると取得できない()
    {
        await using var harness = await CreateRepositoryAsync();
        var unit = PlcUnit.Create("user-3", "C-03", new PlcUnitDraft { Name = "PLC-3", Manufacturer = "KEYENCE", GatewayHost = "127.0.0.1", GatewayPort = 8000 });
        var saved = await harness.Repository.AddAsync(unit);

        var deleted = await harness.Repository.DeleteAsync(saved.Id);

        Assert.IsTrue(deleted);
        var list = await harness.Repository.ListAsync("C-03");
        Assert.AreEqual(0, list.Count);
    }

    private static async Task<RepositoryHarness> CreateRepositoryAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ChatDbContext(options);
        var initializer = new SqliteDatabaseInitializer(context);
        await initializer.InitializeAsync();
        var repository = new PlcUnitRepository(context);
        return new RepositoryHarness(connection, context, repository);
    }

    private sealed class RepositoryHarness : IAsyncDisposable
    {
        public RepositoryHarness(SqliteConnection connection, ChatDbContext context, PlcUnitRepository repository)
        {
            Connection = connection;
            Context = context;
            Repository = repository;
        }

        public SqliteConnection Connection { get; }
        public ChatDbContext Context { get; }
        public PlcUnitRepository Repository { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
