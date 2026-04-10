using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PhotoDrop;
using PhotoDrop.Components;
using PhotoDrop.Data;
using PhotoDrop.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

// ===== Db =====
builder.Services.AddDbContext<PhotoDropContext>(options =>
    options.UseSqlite("Data Source=photodrop.db"));


builder.Services.AddSingleton<GoogleDriveService>();
builder.Services.AddSingleton<QrCodeService>();
builder.Services.AddSingleton<CloudStorageService>();
builder.Services.AddScoped<EventStorage>();
builder.Services.AddScoped<GuestSessionService>();
builder.Services.AddHttpClient<TokenService>();

// ===== Authentication + Google OAuth =====
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");

    options.SaveTokens = true;
    options.AccessType = "offline";

    // Forces consent screen so we always get a refresh token
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        context.Response.Redirect(context.RedirectUri + "&prompt=consent");
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});

// ===== Rate Limiting =====
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        // Presigned URL requests — one per photo, generous for large uploads
        new RateLimitRule { Endpoint = "POST:/api/upload-url/*", Period = "1m", Limit = 120 },
        // Transfer to Drive — only called once per upload batch
        new RateLimitRule { Endpoint = "POST:/api/transfer/*", Period = "1m", Limit = 10 },
        // Storage check — only called once when host creates an event
        new RateLimitRule { Endpoint = "GET:/api/storage/*", Period = "1m", Limit = 10 },
        // Catch-all — covers page loads, assets, and all other endpoints
        new RateLimitRule { Endpoint = "*", Period = "1m", Limit = 200 }
    };
});

var app = builder.Build();

// Ensure database is created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PhotoDropContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseIpRateLimiting();

app.MapPhotoDropEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();