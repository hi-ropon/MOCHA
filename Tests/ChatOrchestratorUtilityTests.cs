using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Services.Chat;

namespace MOCHA.Tests
{
    [TestClass]
    public class ChatOrchestratorUtilityTests
    {
        [TestMethod]
        public void ReadInt_非数値文字列はnullを返す()
        {
            var payload = new Dictionary<string, object?> { ["value"] = "abc" };

            var result = (int?)InvokePrivate("ReadInt", payload, "value");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadInt_数値以外のJsonElementはnullを返す()
        {
            var json = JsonDocument.Parse("{\"v\":true}").RootElement.GetProperty("v");
            var payload = new Dictionary<string, object?> { ["value"] = json };

            var result = (int?)InvokePrivate("ReadInt", payload, "value");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadInt_キーが無ければnullを返す()
        {
            var payload = new Dictionary<string, object?>();

            var result = (int?)InvokePrivate("ReadInt", payload, "missing");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadStringList_数値Json配列は空リストを返す()
        {
            var json = JsonDocument.Parse("[1,2]").RootElement;
            var payload = new Dictionary<string, object?> { ["items"] = json };

            var result = (List<string>?)InvokePrivate("ReadStringList", payload, "items");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ReadStringList_空白のみの文字列はnullを返す()
        {
            var payload = new Dictionary<string, object?> { ["items"] = "   " };

            var result = (List<string>?)InvokePrivate("ReadStringList", payload, "items");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadStringList_キーが無ければnullを返す()
        {
            var payload = new Dictionary<string, object?>();

            var result = (List<string>?)InvokePrivate("ReadStringList", payload, "missing");

            Assert.IsNull(result);
        }

        private static object? InvokePrivate(string methodName, IReadOnlyDictionary<string, object?> payload, string key)
        {
            var method = typeof(ChatOrchestrator).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"{methodName} を取得できませんでした。");

            return method.Invoke(null, new object[] { payload, key });
        }
    }
}
