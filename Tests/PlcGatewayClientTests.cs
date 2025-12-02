using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// PLCゲートウェイクライアントのHTTP通信ロジックを検証する
/// </summary>
[TestClass]
public class PlcGatewayClientTests
{
    /// <summary>
    /// 単体読み取りレスポンスを正しくパースすることを確認
    /// </summary>
    [TestMethod]
    public async Task 単体読み取り_レスポンスを結果化する()
    {
        var handler = new StubHandler(async req =>
        {
            Assert.AreEqual("/api/read", req.RequestUri!.AbsolutePath);
            var body = await req.Content!.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.AreEqual("M", doc.RootElement.GetProperty("device").GetString());
            Assert.AreEqual(10, doc.RootElement.GetProperty("addr").GetInt32());
            Assert.AreEqual(2, doc.RootElement.GetProperty("length").GetInt32());

            var payload = JsonSerializer.Serialize(new { values = new[] { 1, 0 }, success = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var client = new PlcGatewayClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") }, new DummyLogger());
        var result = await client.ReadAsync(new DeviceReadRequest("M10:2", BaseUrl: null), CancellationToken.None);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<int> { 1, 0 }, (System.Collections.ICollection)result.Values!);
        Assert.AreEqual("M10", result.Device);
    }

    /// <summary>
    /// バッチ読み取りレスポンスを正しくパースすることを確認
    /// </summary>
    [TestMethod]
    public async Task バッチ読み取り_複数結果を返す()
    {
        var handler = new StubHandler(async req =>
        {
            Assert.AreEqual("/api/batch_read", req.RequestUri!.AbsolutePath);
            var payload = JsonSerializer.Serialize(new
            {
                results = new[]
                {
                    new { device = "D100", values = new[]{ 10 }, success = true, error = (string?)null },
                    new { device = "M10", values = Array.Empty<int>(), success = false, error = "fail" }
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var client = new PlcGatewayClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") }, new DummyLogger());
        var result = await client.ReadBatchAsync(new BatchReadRequest(new[] { "D100", "M10" }, BaseUrl: null), CancellationToken.None);

        Assert.AreEqual(2, result.Results.Count);
        Assert.IsTrue(result.Results[0].Success);
        Assert.AreEqual("D100", result.Results[0].Device);
        Assert.IsFalse(result.Results[1].Success);
        Assert.AreEqual("fail", result.Results[1].Error);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }

    private sealed class DummyLogger : Microsoft.Extensions.Logging.ILogger<PlcGatewayClient>
    {
        public IDisposable BeginScope<TState>(TState state) => NullScope.instance;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope instance = new();
            public void Dispose() { }
        }
    }
}
