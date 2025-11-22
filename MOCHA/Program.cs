using MOCHA.Components;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<CopilotStudioOptions>(builder.Configuration.GetSection("Copilot"));
builder.Services.AddHttpClient("CopilotStudio");
builder.Services.AddScoped<ICopilotChatClient, CopilotStudioChatClient>();
builder.Services.Configure<PlcGatewayOptions>(builder.Configuration.GetSection("PlcGateway"));
builder.Services.AddHttpClient<HttpPlcGatewayClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PlcGatewayOptions>>().Value;
    if (Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri))
    {
        client.BaseAddress = uri;
    }
    client.Timeout = options.Timeout;
});
builder.Services.AddScoped<IPlcGatewayClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlcGatewayOptions>>().Value;
    if (options.Enabled)
    {
        return sp.GetRequiredService<HttpPlcGatewayClient>();
    }

    return new FakePlcGatewayClient();
});
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();
builder.Services.AddScoped<ConversationHistoryState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
