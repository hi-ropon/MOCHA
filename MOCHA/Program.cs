using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using MOCHA.Components;
using MOCHA.Data;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;

var builder = WebApplication.CreateBuilder(args);

var azureAdEnabled = builder.Configuration.GetValue<bool>("AzureAd:Enabled");

if (azureAdEnabled)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = options.DefaultPolicy;
    });
}
else
{
    builder.Services.AddAuthorization(options =>
    {
        var allowAnonymous = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
        options.DefaultPolicy = allowAnonymous;
        options.FallbackPolicy = allowAnonymous;
    });
}

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ChatDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ChatDb") ?? "Data Source=chat.db";
    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddMicrosoftIdentityConsentHandler();

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
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();
builder.Services.AddScoped<ConversationHistoryState>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
