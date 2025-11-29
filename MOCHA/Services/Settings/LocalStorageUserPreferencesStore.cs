using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ブラウザの localStorage を使ってユーザー設定を保持する。
/// </summary>
public sealed class LocalStorageUserPreferencesStore : IUserPreferencesStore
{
    private readonly IJSRuntime _jsRuntime;
    private const string _storageKey = "mocha.preferences";

    public LocalStorageUserPreferencesStore(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    public async Task<UserPreferences?> GetAsync(CancellationToken cancellationToken = default)
    {
        string? json;
        try
        {
            json = await _jsRuntime.InvokeAsync<string?>(
                "mochaPreferences.getStoredPreferences",
                cancellationToken,
                _storageKey);
        }
        catch (JSException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PreferencePayload>(json);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Theme))
            {
                return null;
            }

            if (Enum.TryParse<Theme>(payload.Theme, ignoreCase: true, out var theme))
            {
                return new UserPreferences(theme);
            }
        }
        catch (JsonException)
        {
            // 格納形式が壊れている場合は null を返して初期化する。
        }

        return null;
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        var payload = new PreferencePayload { Theme = preferences.Theme.ToString().ToLowerInvariant() };
        var json = JsonSerializer.Serialize(payload);

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "mochaPreferences.savePreferences",
                cancellationToken,
                _storageKey,
                json);
        }
        catch (JSException)
        {
            // JS 側が未初期化の場合は無視する。
        }
    }

    private sealed class PreferencePayload
    {
        public string Theme { get; set; } = "light";
    }
}
