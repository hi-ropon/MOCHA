using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Agents;
using MOCHA.Services.Agents;

namespace MOCHA.Tests;

/// <summary>AgentSettingsSelection の選択ロジック検証</summary>
[TestClass]
public class AgentSettingsSelectionTests
{
    /// <summary>初期化時に優先番号が存在すればそれを選択する</summary>
    [TestMethod]
    public void 初期化_優先番号が存在すればそれを選択する()
    {
        var selection = new AgentSettingsSelection();
        var agents = new List<DeviceAgentProfile>
        {
            new("A-01", "ライン1", DateTimeOffset.UtcNow),
            new("B-02", "ライン2", DateTimeOffset.UtcNow)
        };

        var changed = selection.Reset(agents, "B-02");

        Assert.IsTrue(changed);
        Assert.AreEqual("B-02", selection.SelectedAgentNumber);
    }

    /// <summary>初期化時に優先番号が無い場合は先頭を選択する</summary>
    [TestMethod]
    public void 初期化_優先番号が無い場合は先頭を選択する()
    {
        var selection = new AgentSettingsSelection();
        var agents = new List<DeviceAgentProfile>
        {
            new("A-01", "ライン1", DateTimeOffset.UtcNow),
            new("B-02", "ライン2", DateTimeOffset.UtcNow)
        };

        var changed = selection.Reset(agents, "Z-99");

        Assert.IsTrue(changed);
        Assert.AreEqual("A-01", selection.SelectedAgentNumber);
    }

    /// <summary>存在する番号を選ぶと選択状態が更新される</summary>
    [TestMethod]
    public void 選択_存在する番号で選択が更新される()
    {
        var selection = new AgentSettingsSelection();
        var agents = new List<DeviceAgentProfile>
        {
            new("A-01", "ライン1", DateTimeOffset.UtcNow),
            new("B-02", "ライン2", DateTimeOffset.UtcNow)
        };
        selection.Reset(agents, "A-01");

        var changed = selection.Select(agents, "B-02");

        Assert.IsTrue(changed);
        Assert.AreEqual("B-02", selection.SelectedAgentNumber);
    }

    /// <summary>存在しない番号は無視して選択状態を維持する</summary>
    [TestMethod]
    public void 選択_存在しない番号では変更されない()
    {
        var selection = new AgentSettingsSelection();
        var agents = new List<DeviceAgentProfile>
        {
            new("A-01", "ライン1", DateTimeOffset.UtcNow),
            new("B-02", "ライン2", DateTimeOffset.UtcNow)
        };
        selection.Reset(agents, "A-01");

        var changed = selection.Select(agents, "Z-99");

        Assert.IsFalse(changed);
        Assert.AreEqual("A-01", selection.SelectedAgentNumber);
    }

    /// <summary>一覧が空の場合は選択を解除する</summary>
    [TestMethod]
    public void 初期化_一覧が空なら選択が解除される()
    {
        var selection = new AgentSettingsSelection();
        var agents = new List<DeviceAgentProfile>
        {
            new("A-01", "ライン1", DateTimeOffset.UtcNow)
        };
        selection.Reset(agents, "A-01");

        var changed = selection.Reset(Array.Empty<DeviceAgentProfile>(), "A-01");

        Assert.IsTrue(changed);
        Assert.IsNull(selection.SelectedAgentNumber);
    }
}
