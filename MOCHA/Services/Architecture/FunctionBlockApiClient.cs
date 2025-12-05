using System.Net.Http.Headers;
using System.Text.Json;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ファンクションブロックAPI呼び出しクライアント
/// </summary>
public sealed class FunctionBlockApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// HTTPクライアントファクトリ注入による初期化
    /// </summary>
    public FunctionBlockApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// ユニット一覧取得
    /// </summary>
    public async Task<IReadOnlyList<PlcUnitSummary>> ListUnitsAsync(string agentNumber, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        var uri = $"api/plc-units?agentNumber={Uri.EscapeDataString(agentNumber)}";
        var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<PlcUnitSummary>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var items = await JsonSerializer.DeserializeAsync<List<PlcUnitSummary>>(stream, _jsonOptions, cancellationToken);
        return items ?? new List<PlcUnitSummary>();
    }

    /// <summary>
    /// 一覧取得
    /// </summary>
    public async Task<IReadOnlyList<FunctionBlockSummary>> ListAsync(Guid plcUnitId, string agentNumber, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        var uri = $"api/plc-units/{plcUnitId}/function-blocks?agentNumber={Uri.EscapeDataString(agentNumber)}";
        var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<FunctionBlockSummary>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var items = await JsonSerializer.DeserializeAsync<List<FunctionBlockSummary>>(stream, _jsonOptions, cancellationToken);
        return items ?? new List<FunctionBlockSummary>();
    }

    /// <summary>
    /// 削除
    /// </summary>
    public async Task<bool> DeleteAsync(Guid plcUnitId, Guid functionBlockId, string agentNumber, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        var uri = $"api/plc-units/{plcUnitId}/function-blocks/{functionBlockId}?agentNumber={Uri.EscapeDataString(agentNumber)}";
        var response = await client.DeleteAsync(uri, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// アップロード
    /// </summary>
    public async Task<bool> UploadAsync(
        Guid plcUnitId,
        string agentNumber,
        string name,
        Stream labelStream,
        string labelFileName,
        Stream programStream,
        string programFileName,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(agentNumber), "AgentNumber");
        content.Add(new StringContent(name), "Name");

        var labelContent = new StreamContent(labelStream);
        labelContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(labelContent, "LabelFile", labelFileName);

        var programContent = new StreamContent(programStream);
        programContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(programContent, "ProgramFile", programFileName);

        var uri = $"api/plc-units/{plcUnitId}/function-blocks";
        var response = await client.PostAsync(uri, content, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

/// <summary>
/// ファンクションブロック一覧用の簡易ビュー
/// </summary>
public sealed class FunctionBlockSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SafeName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public FileSummary? Label { get; set; }
    public FileSummary? Program { get; set; }
}

/// <summary>
/// ファイルメタ簡易ビュー
/// </summary>
public sealed class FileSummary
{
    public string? FileName { get; set; }
    public string? RelativePath { get; set; }
}

/// <summary>
/// PLCユニット一覧用ビュー
/// </summary>
public sealed class PlcUnitSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AgentNumber { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Role { get; set; }
}
