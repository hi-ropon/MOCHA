using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Services.Markdown;

namespace MOCHA.Tests
{
    /// <summary>
    /// Markdownレンダラーの変換テスト
    /// </summary>
    [TestClass]
    public class MarkdownRendererTests
    {
        /// <summary>
        /// 太字記法をHTMLに変換する
        /// </summary>
        [TestMethod]
        public void 太字をHTMLへ変換する()
        {
            var renderer = new MarkdownRenderer();

            var html = renderer.Render("これは **太字** です");

            StringAssert.Contains(html, "<strong>太字</strong>");
            StringAssert.StartsWith(html.Trim(), "<p>");
        }

        /// <summary>
        /// スクリプトタグを除去する
        /// </summary>
        [TestMethod]
        public void スクリプトがサニタイズされる()
        {
            var renderer = new MarkdownRenderer();

            var html = renderer.Render("<script>alert('xss')</script><b>safe</b>");

            Assert.IsFalse(html.Contains("<script", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(html, "<b>safe</b>");
        }

        /// <summary>
        /// コードブロックの言語クラスを保持する
        /// </summary>
        [TestMethod]
        public void コードブロックのクラスを保持する()
        {
            var renderer = new MarkdownRenderer();

            var html = renderer.Render("```csharp\nConsole.WriteLine(\"test\");\n```");

            StringAssert.Contains(html, "<code class=\"language-csharp\"");
            StringAssert.Contains(html, "Console.WriteLine(\"test\");");
        }
    }
}
