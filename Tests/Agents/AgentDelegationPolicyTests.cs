using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain;

namespace MOCHA.Tests;

/// <summary>
/// エージェント委譲ポリシーの検証
/// </summary>
[TestClass]
public class AgentDelegationPolicyTests
{
    /// <summary>
    /// 既定設定で organizer から全エージェントへ委譲可能であることを確認
    /// </summary>
    [TestMethod]
    public void 既定設定_organizerから全エージェントを許可()
    {
        var policy = new AgentDelegationPolicy(new AgentDelegationOptions());

        var callees = policy.GetAllowedCallees("organizer");

        Assert.AreEqual(4, callees.Count);
        Assert.IsTrue(callees.Contains("plcAgent"));
        Assert.IsTrue(callees.Contains("iaiAgent"));
        Assert.IsTrue(callees.Contains("orientalAgent"));
        Assert.IsTrue(callees.Contains("drawingAgent"));
    }

    /// <summary>
    /// PLC エージェントから IAI が許可されることを確認
    /// </summary>
    [TestMethod]
    public void PlcエージェントからIaiを許可()
    {
        var policy = new AgentDelegationPolicy(new AgentDelegationOptions());

        var allowed = policy.CanInvoke("plcAgent", "iaiAgent", currentDepth: 1, out var reason);

        Assert.IsTrue(allowed);
        Assert.IsNull(reason);
    }

    /// <summary>
    /// 深さ上限を超える場合は拒否することを確認
    /// </summary>
    [TestMethod]
    public void 深さ上限超過時_委譲不可()
    {
        var policy = new AgentDelegationPolicy(new AgentDelegationOptions { MaxDepth = 2 });

        var allowed = policy.CanInvoke("organizer", "plcAgent", currentDepth: 2, out var reason);

        Assert.IsFalse(allowed);
        StringAssert.Contains(reason, "上限");
    }

    /// <summary>
    /// 自己呼び出しは拒否することを確認
    /// </summary>
    [TestMethod]
    public void 自己呼び出し_委譲不可()
    {
        var policy = new AgentDelegationPolicy(new AgentDelegationOptions());

        var allowed = policy.CanInvoke("plcAgent", "plcAgent", currentDepth: 1, out var reason);

        Assert.IsFalse(allowed);
        StringAssert.Contains(reason, "同一エージェント");
    }
}
