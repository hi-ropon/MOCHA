using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Factories;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// DrawingRepository のDB永続化テスト
/// </summary>
[TestClass]
public class DrawingRepositoryTests
{
    /// <summary>
    /// 追加した図面を一覧取得できることを確認
    /// </summary>
    [TestMethod]
    public async Task 追加した図面を一覧取得できる()
    {
        await using var harness = await CreateRepositoryAsync();
        var document = DrawingDocument.Create(
            "user-1",
            "A-01",
            "layout.pdf",
            "application/pdf",
            1024,
            "初版",
            relativePath: "A-01/layout.pdf",
            storageRoot: "C:/DrawingStorage");

        await harness.Repository.AddAsync(document);

        var list = await harness.Repository.ListAsync("user-1", "A-01");

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("layout.pdf", list[0].FileName);
        Assert.AreEqual("A-01/layout.pdf", list[0].RelativePath);
        Assert.AreEqual("C:/DrawingStorage", list[0].StorageRoot);
    }

    /// <summary>
    /// 説明の更新が保存されることを確認
    /// </summary>
    [TestMethod]
    public async Task 説明更新が保存される()
    {
        await using var harness = await CreateRepositoryAsync();
        var document = DrawingDocument.Create(
            "user-2",
            "B-02",
            "panel.dwg",
            "application/octet-stream",
            2048,
            "初回図面");
        var saved = await harness.Repository.AddAsync(document);

        var updated = await harness.Repository.UpdateAsync(saved.WithDescription("承認版"));

        Assert.AreEqual("承認版", updated.Description);

        var fetched = await harness.Repository.GetAsync(saved.Id);
        Assert.AreEqual("承認版", fetched!.Description);
    }

    /// <summary>
    /// 削除後は一覧に含まれないことを確認
    /// </summary>
    [TestMethod]
    public async Task 削除すると一覧から消える()
    {
        await using var harness = await CreateRepositoryAsync();
        var document = DrawingDocument.Create(
            "user-3",
            "C-03",
            "delete.pdf",
            "application/pdf",
            512,
            null);
        var saved = await harness.Repository.AddAsync(document);

        var deleted = await harness.Repository.DeleteAsync(saved.Id);

        Assert.IsTrue(deleted);
        var list = await harness.Repository.ListAsync("user-3", "C-03");
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
        var repository = new DrawingRepository(context);
        return new RepositoryHarness(connection, context, repository);
    }

    private sealed class RepositoryHarness : IAsyncDisposable
    {
        public RepositoryHarness(SqliteConnection connection, ChatDbContext context, DrawingRepository repository)
        {
            Connection = connection;
            Context = context;
            Repository = repository;
        }

        public SqliteConnection Connection { get; }
        public ChatDbContext Context { get; }
        public DrawingRepository Repository { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
