using HOMEOWNER.Configuration;
using HOMEOWNER.Data;
using HOMEOWNER.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var firebaseProjectId = builder.Configuration["Firebase:ProjectId"] ?? "homeowner-c355d";
var isDevelopment = builder.Environment.IsDevelopment();

if (isDevelopment && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
{
    var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    var firebaseKeyFile = Directory.GetFiles(downloadsPath, $"*{firebaseProjectId}-firebase*.json")
        .FirstOrDefault();

    if (firebaseKeyFile != null && File.Exists(firebaseKeyFile))
    {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", firebaseKeyFile);
        Console.WriteLine($"Auto-detected Firebase credentials: {firebaseKeyFile}");
    }
    else
    {
        Console.WriteLine("WARNING: GOOGLE_APPLICATION_CREDENTIALS not set and Firebase key file not found.");
        Console.WriteLine("Set it manually: $env:GOOGLE_APPLICATION_CREDENTIALS=\"C:\\path\\to\\key.json\"");
    }
}

builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection("BootstrapAdmin"));
builder.Services.Configure<FirebaseAuthenticationOptions>(builder.Configuration.GetSection("FirebaseAuthentication"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.Configure<SupabaseStorageOptions>(builder.Configuration.GetSection("SupabaseStorage"));
builder.Services.Configure<AppFileStorageOptions>(builder.Configuration.GetSection("AppFileStorage"));
builder.Services.AddSingleton<IFirebaseAdminAppProvider, FirebaseAdminAppProvider>();
builder.Services.AddSingleton<IAppPasswordHasher, AppPasswordHasher>();
builder.Services.AddHttpClient<IUserIdentityService, FirebaseUserIdentityService>();
builder.Services.AddHttpClient<ISupabaseObjectStorageService, SupabaseObjectStorageService>();
builder.Services.AddScoped<IAppFileStorageService, AppFileStorageService>();
builder.Services.AddScoped<SupabaseProfileImageStorageService>();
builder.Services.AddScoped<IProfileImageStorageService, ResilientProfileImageStorageService>();
builder.Services.AddSingleton<FirebaseService>();
builder.Services.AddSingleton<IDataService>(sp => sp.GetRequiredService<FirebaseService>());
builder.Services.AddScoped<ICommunityNotificationService, CommunityNotificationService>();
builder.Services.AddHostedService<AdminBootstrapHostedService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer("Server=.;Database=TempDb;Trusted_Connection=true;TrustServerCertificate=true"));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = "__Host-RestNestHome.Session";
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Name = "__Host-RestNestHome.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Security headers + minimal CSP
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "0";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    // NOTE: This CSP is intentionally permissive enough to support the current app UI
    // (inline scripts in some Razor views + external CDNs). Tighten later by removing
    // inline scripts and pinning to a smaller allowlist (or use nonces).
    ctx.Response.Headers["Content-Security-Policy"] = string.Join(" ", new[]
    {
        "default-src 'self';",
        "base-uri 'self';",
        "object-src 'none';",
        "frame-ancestors 'none';",
        "form-action 'self';",
        "img-src 'self' data: https://*.supabase.co;",
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://fonts.googleapis.com;",
        "font-src 'self' data: https://cdnjs.cloudflare.com https://fonts.gstatic.com;",
        "script-src 'self' 'unsafe-inline' https://code.jquery.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://esm.sh;",
        "connect-src 'self' https://*.firebaseio.com https://identitytoolkit.googleapis.com https://*.supabase.co https://www.iprogsms.com;"
    });
    await next();
});

var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
if (!isDevelopment)
{
    if (string.IsNullOrWhiteSpace(credentialPath) || !File.Exists(credentialPath))
    {
        throw new InvalidOperationException("GOOGLE_APPLICATION_CREDENTIALS must point to a valid Firebase service account JSON file in production.");
    }

    var contentRootPath = app.Environment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var fullCredentialPath = Path.GetFullPath(credentialPath);
    if (fullCredentialPath.StartsWith(contentRootPath, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Firebase service account credentials must not be stored inside the deployed application directory.");
    }

    var bootstrapEnabled = builder.Configuration.GetValue<bool>("BootstrapAdmin:Enabled");
    if (bootstrapEnabled)
    {
        throw new InvalidOperationException("BootstrapAdmin must be disabled in production.");
    }

    var firebaseWebApiKey = builder.Configuration["FirebaseAuthentication:WebApiKey"];
    if (string.IsNullOrWhiteSpace(firebaseWebApiKey))
    {
        throw new InvalidOperationException("FirebaseAuthentication:WebApiKey must be configured in production.");
    }

    var supabaseUrl = builder.Configuration["SupabaseStorage:ProjectUrl"]
        ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
    var supabaseServiceRoleKey = builder.Configuration["SupabaseStorage:ServiceRoleKey"]
        ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

    if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseServiceRoleKey))
    {
        throw new InvalidOperationException("Supabase Storage must be configured in production for application uploads. Provide SupabaseStorage:ProjectUrl and SupabaseStorage:ServiceRoleKey, or SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
