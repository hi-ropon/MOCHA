using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Services.Auth;

namespace MOCHA.Tests
{
    [TestClass]
    public class RoleSettingsServiceTests
    {
        [TestMethod]
        public void NormalizeUserId_空白のみはnull()
        {
            Assert.IsNull(RoleSettingsService.NormalizeUserId("   "));
            Assert.AreEqual("abc", RoleSettingsService.NormalizeUserId(" abc "));
        }

        [TestMethod]
        public void CalculateDiff_追加と削除を判定する()
        {
            var current = new[] { "admin", "dev" };
            var selected = new[] { "dev", "op" };

            var diff = RoleSettingsService.CalculateDiff(current, selected);

            CollectionAssert.AreEquivalent(new[] { "op" }, (System.Collections.ICollection)diff.ToAssign);
            CollectionAssert.AreEquivalent(new[] { "admin" }, (System.Collections.ICollection)diff.ToRemove);
        }

        [TestMethod]
        public void Toggle_選択状態を更新する()
        {
            var selected = new HashSet<string>(new[] { "admin" });

            var afterAdd = RoleSettingsService.Toggle(selected, "dev", true);
            Assert.IsTrue(afterAdd.Contains("admin"));
            Assert.IsTrue(afterAdd.Contains("dev"));

            var afterRemove = RoleSettingsService.Toggle(afterAdd, "admin", false);
            Assert.IsFalse(afterRemove.Contains("admin"));
            Assert.IsTrue(afterRemove.Contains("dev"));
        }
    }
}
