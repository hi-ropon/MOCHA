using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;

namespace Tests.Chat;

/// <summary>
/// ChatAttachmentService の基本動作検証
/// </summary>
[TestClass]
public class ChatAttachmentServiceTests
{
    [TestMethod]
    public async Task アップロード_正常系_Base64が生成される()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var service = new ChatAttachmentService(new NullLogger<ChatAttachmentService>());
        var file = new FakeBrowserFile("test.png", "image/png", content);

        var attachment = await service.UploadAsync(file, CancellationToken.None);

        Assert.IsFalse(string.IsNullOrWhiteSpace(attachment.Id), "ID がセットされること");
        StringAssert.StartsWith(attachment.SmallBase64, "data:image/png;base64", "PNG の data URL になること");
        Assert.AreEqual(content.LongLength, attachment.Size, "サイズが一致すること");
    }

    [TestMethod]
    public async Task アップロード_サイズ超過_例外になる()
    {
        var oversized = new byte[10 * 1024 * 1024 + 1];
        var service = new ChatAttachmentService(new NullLogger<ChatAttachmentService>());
        var file = new FakeBrowserFile("big.jpg", "image/jpeg", oversized);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.UploadAsync(file, CancellationToken.None));
    }

    [TestMethod]
    public async Task アップロード_非対応形式_例外になる()
    {
        var content = new byte[] { 1, 2, 3 };
        var service = new ChatAttachmentService(new NullLogger<ChatAttachmentService>());
        var file = new FakeBrowserFile("doc.txt", "text/plain", content);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.UploadAsync(file, CancellationToken.None));
    }

    [TestMethod]
    public async Task バイト配列アップロード_正常系()
    {
        var content = new byte[] { 9, 8, 7 };
        var service = new ChatAttachmentService(new NullLogger<ChatAttachmentService>());

        var attachment = await service.UploadAsync("paste.png", "image/png", content, CancellationToken.None);

        Assert.AreEqual(content.LongLength, attachment.Size, "サイズが一致すること");
        StringAssert.StartsWith(attachment.SmallBase64, "data:image/png;base64", "DataURL で返ること");
    }

    private sealed class FakeBrowserFile : IBrowserFile
    {
        private readonly byte[] _content;

        public FakeBrowserFile(string name, string contentType, byte[] content)
        {
            Name = name;
            ContentType = contentType;
            _content = content;
            Size = content.LongLength;
        }

        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;

        public string Name { get; }

        public string ContentType { get; }

        public long Size { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException("File too large");
            }

            return new MemoryStream(_content);
        }
    }
}
