using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Tests;

/// <summary>
/// デバイスアドレス値オブジェクトを検証する
/// </summary>
[TestClass]
public class DeviceAddressTests
{
    /// <summary>
    /// タイマデバイス指定をTSに正規化することを確認
    /// </summary>
    [TestMethod]
    public void タイマ指定_Tで入力_TSに正規化される()
    {
        var address = DeviceAddress.Parse("T0");

        Assert.AreEqual("TS", address.Device);
        Assert.AreEqual("0", address.Address);
        Assert.AreEqual("TS0", address.Display);
        Assert.AreEqual("TS0", address.ToSpec());
    }

    /// <summary>
    /// 長さ指定を保持してSpecを再構成することを確認
    /// </summary>
    [TestMethod]
    public void 長さ指定_コロン付き入力_末尾の長さを維持する()
    {
        var address = DeviceAddress.Parse("M10:2");

        Assert.AreEqual("M", address.Device);
        Assert.AreEqual("10", address.Address);
        Assert.AreEqual(2, address.Length);
        Assert.AreEqual("M10:2", address.ToSpec());
    }
}
