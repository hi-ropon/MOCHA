using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using MOCHA.Agents;
using MOCHA.Components;
using MOCHA.Data;
using MOCHA.Models.Auth;
using MOCHA.Services.Agents;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Plc;
using MOCHA.Factories;
using MOCHA.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

var azureAdEnabled = builder.Configuration.GetValue<bool>("AzureAd:Enabled");
var fakeAuthOptions = builder.Configuration.GetSection("FakeAuth").Get<FakeAuthOptions>() ?? new FakeAuthOptions();

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
    if (fakeAuthOptions.Enabled)
    {
        builder.Services.AddAuthentication(FakeAuthHandler.scheme)
            .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(FakeAuthHandler.scheme, _ => { });

        builder.Services.AddAuthorization(options =>
        {
            var authenticatedOnly = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(FakeAuthHandler.scheme)
                .RequireAuthenticatedUser()
                .Build();
            options.DefaultPolicy = authenticatedOnly;
            options.FallbackPolicy = authenticatedOnly;
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
builder.Services.AddScoped<IDeviceAgentRepository, DeviceAgentRepository>();
builder.Services.AddScoped<DeviceAgentState>();
builder.Services.AddScoped<IUserPreferencesStore, LocalStorageUserPreferencesStore>();
builder.Services.AddScoped<IColorSchemeProvider, BrowserColorSchemeProvider>();
builder.Services.AddScoped<IThemeApplicator, DomThemeApplicator>();
builder.Services.AddScoped<UserPreferencesState>();
builder.Services.AddScoped<IUserRoleProvider, DbUserRoleProvider>();
builder.Services.Configure<RoleBootstrapOptions>(builder.Configuration.GetSection("RoleBootstrap"));
builder.Services.Configure<FakeAuthOptions>(builder.Configuration.GetSection("FakeAuth"));
builder.Services.AddScoped<RoleBootstrapper>();
builder.Services.AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();
builder.Services.AddMochaAgents(builder.Configuration);
builder.Services.AddScoped<IAgentChatClient, AgentOrchestratorChatClient>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<RoleBootstrapper>();
    bootstrapper.EnsureAdminRolesAsync().GetAwaiter().GetResult();
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
