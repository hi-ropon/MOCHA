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
            Assert.AreEqual(HttpMethod.Get, req.Method);
            Assert.AreEqual("/api/read/M/10/2", req.RequestUri!.AbsolutePath);

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
    /// 16進アドレスをそのまま送信することを確認
    /// </summary>
    [TestMethod]
    public async Task 単体読み取り_16進アドレスをそのまま送信する()
    {
        var handler = new StubHandler(async req =>
        {
            Assert.AreEqual(HttpMethod.Get, req.Method);
            Assert.AreEqual("/api/read/X/1A/1", req.RequestUri!.AbsolutePath);

            var payload = JsonSerializer.Serialize(new { values = new[] { 42 }, success = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var client = new PlcGatewayClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") }, new DummyLogger());
        var result = await client.ReadAsync(new DeviceReadRequest("X1A", BaseUrl: null), CancellationToken.None);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<int> { 42 }, (System.Collections.ICollection)result.Values!);
        Assert.AreEqual("X1A", result.Device);
    }

    /// <summary>
    /// ZR の2文字デバイス種別を保持して送信することを確認
    /// </summary>
    [TestMethod]
    public async Task 単体読み取り_ZRデバイスを送信する()
    {
        var handler = new StubHandler(async req =>
        {
            Assert.AreEqual(HttpMethod.Get, req.Method);
            Assert.AreEqual("/api/read/ZR/100/2", req.RequestUri!.AbsolutePath);

            var payload = JsonSerializer.Serialize(new { values = new[] { 7, 8 }, success = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var client = new PlcGatewayClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") }, new DummyLogger());
        var result = await client.ReadAsync(new DeviceReadRequest("ZR100:2", BaseUrl: null), CancellationToken.None);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<int> { 7, 8 }, (System.Collections.ICollection)result.Values!);
        Assert.AreEqual("ZR100", result.Device);
    }

    /// <summary>
    /// plc_host をクエリに含めて送信することを確認
    /// </summary>
    [TestMethod]
    public async Task 単体読み取り_plc_hostクエリを送信する()
    {
        var handler = new StubHandler(async req =>
        {
            Assert.AreEqual(HttpMethod.Get, req.Method);
            StringAssert.Contains(req.RequestUri!.AbsolutePath, "/api/read/D/200/1");
            StringAssert.Contains(req.RequestUri!.Query, "plc_host=plc.edge");
            StringAssert.Contains(req.RequestUri!.Query, "ip=127.0.0.1");
            StringAssert.Contains(req.RequestUri!.Query, "port=5511");

            var payload = JsonSerializer.Serialize(new { values = new[] { 1234 }, success = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var client = new PlcGatewayClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") }, new DummyLogger());
        var result = await client.ReadAsync(new DeviceReadRequest("D200", Ip: "127.0.0.1", Port: 5511, PlcHost: "plc.edge", BaseUrl: null), CancellationToken.None);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<int> { 1234 }, (System.Collections.ICollection)result.Values!);
        Assert.AreEqual("D200", result.Device);
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
            var json = await req.Content!.ReadAsStringAsync();
            StringAssert.Contains(json, "\"devices\"");
            StringAssert.Contains(json, "\"D100\"");
            Assert.IsFalse(json.Contains("\"plc_host\":null"));
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
