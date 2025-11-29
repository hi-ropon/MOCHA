using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
using MOCHA.Services.Drawings;
using MOCHA.Services.Architecture;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var azureAdEnabled = builder.Configuration.GetValue<bool>("AzureAd:Enabled");
var devAuthOptions = builder.Configuration.GetSection("DevAuth").Get<DevAuthOptions>() ?? new DevAuthOptions();
var configuredDefaultScheme = builder.Configuration["Authentication:DefaultScheme"];
var configuredChallengeScheme = builder.Configuration["Authentication:DefaultChallengeScheme"];

var chosenDefaultScheme = configuredDefaultScheme ?? (azureAdEnabled ? OpenIdConnectDefaults.AuthenticationScheme : DevAuthDefaults.scheme);
var chosenChallengeScheme = configuredChallengeScheme ?? chosenDefaultScheme;

var cookieLifetime = TimeSpan.FromHours(Math.Max(1, devAuthOptions.ExpireHours));

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = chosenDefaultScheme;
        options.DefaultChallengeScheme = chosenChallengeScheme;
    })
    .AddCookie(DevAuthDefaults.scheme, options =>
    {
        options.LoginPath = devAuthOptions.LoginPath;
        options.LogoutPath = devAuthOptions.LogoutPath;
        options.AccessDeniedPath = devAuthOptions.AccessDeniedPath;
        options.Cookie.Name = devAuthOptions.CookieName;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = cookieLifetime;
    });

if (azureAdEnabled)
{
    authBuilder.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization(options =>
{
    if (azureAdEnabled || devAuthOptions.Enabled)
    {
        var authenticatedOnly = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        options.DefaultPolicy = authenticatedOnly;
        options.FallbackPolicy = authenticatedOnly;
    }
    else
    {
        var allowAnonymous = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
        options.DefaultPolicy = allowAnonymous;
        options.FallbackPolicy = allowAnonymous;
    }
});

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
builder.Services.AddSingleton<IDrawingRepository, InMemoryDrawingRepository>();
builder.Services.AddScoped<DrawingRegistrationService>();
builder.Services.AddSingleton<IPlcUnitRepository, InMemoryPlcUnitRepository>();
builder.Services.AddScoped<PlcConfigurationService>();
builder.Services.AddScoped<IUserPreferencesStore, LocalStorageUserPreferencesStore>();
builder.Services.AddScoped<IColorSchemeProvider, BrowserColorSchemeProvider>();
builder.Services.AddScoped<IThemeApplicator, DomThemeApplicator>();
builder.Services.AddScoped<UserPreferencesState>();
builder.Services.AddScoped<IUserRoleProvider, DbUserRoleProvider>();
builder.Services.Configure<RoleBootstrapOptions>(builder.Configuration.GetSection("RoleBootstrap"));
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection("DevAuth"));
builder.Services.AddScoped<RoleBootstrapper>();
builder.Services.AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();
builder.Services.AddMochaAgents(builder.Configuration);
builder.Services.AddScoped<IAgentChatClient, AgentOrchestratorChatClient>();
builder.Services.AddScoped<IDevLoginService, DevLoginService>();
builder.Services.AddScoped<IDevUserService, DevUserService>();
builder.Services.AddScoped<IPasswordHasher<DevUserEntity>, PasswordHasher<DevUserEntity>>();

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

public partial class Program;
