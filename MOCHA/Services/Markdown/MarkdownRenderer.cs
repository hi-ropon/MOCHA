using System;
using Ganss.Xss;
using Markdig;

namespace MOCHA.Services.Markdown;

/// <summary>
/// MarkdownをHTMLに変換しサニタイズするレンダラー
/// </summary>
public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _sanitizer = CreateSanitizer();
    }

    public string Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.UnionWith(new[]
        {
            "p", "ul", "ol", "li", "pre", "code", "blockquote",
            "table", "thead", "tbody", "tr", "th", "td",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "em", "strong", "hr", "br"
        });

        sanitizer.AllowedAttributes.Add("class");

        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttp);
        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttps);

        return sanitizer;
    }
}
