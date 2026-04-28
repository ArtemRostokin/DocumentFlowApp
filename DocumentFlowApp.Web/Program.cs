using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Infrastructure.Repositories;
using DocumentFlowApp.Infrastructure.Services;
using DocumentFlowApp.Services;
using DocumentFlowApp.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Text;

var crashLogDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "DocumentFlowAppLogs");
Directory.CreateDirectory(crashLogDir);
var crashLogPath = Path.Combine(crashLogDir, "web-crash.log");
var traceLogPath = Path.Combine(crashLogDir, "web-trace.log");

void AppendCrashLog(string title, string details)
{
    try
    {
        var line = $"[{DateTime.UtcNow:O}] {title}{Environment.NewLine}{details}{Environment.NewLine}--------------------{Environment.NewLine}";
        File.AppendAllText(crashLogPath, line);
    }
    catch
    {
        // no-op: ?????? ?????? ??? ??????? ???????? ??? ???????
    }
}

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    AppendCrashLog("AppDomain.UnhandledException", args.ExceptionObject?.ToString() ?? "unknown");
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    AppendCrashLog("AppDomain.ProcessExit", "Process is exiting.");
};

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    AppendCrashLog("TaskScheduler.UnobservedTaskException", args.Exception.ToString());
    args.SetObserved();
};

void AppendTrace(string message)
{
    try
    {
        File.AppendAllText(traceLogPath, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
    }
    catch
    {
        // no-op
    }
}

AppendTrace("Program started");

try
{
    var builder = WebApplication.CreateBuilder(args);
    AppendTrace("Builder created");

    builder.Services.AddControllersWithViews();
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        throw new InvalidOperationException("JWT key is not configured. Set Jwt:Key in configuration.");

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue("df_auth_token", out var token) && !string.IsNullOrWhiteSpace(token))
                        context.Token = token;

                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    var returnUrl = context.Request.Path + context.Request.QueryString;
                    context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            policy.RequireAssertion(context =>
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Admin)));

        options.AddPolicy(AuthorizationPolicies.ManagerOrAdmin, policy =>
            policy.RequireAssertion(context =>
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Admin) ||
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Manager)));

        options.AddPolicy(AuthorizationPolicies.EmployeeOrHigher, policy =>
            policy.RequireAssertion(context =>
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Admin) ||
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Manager) ||
                AuthorizationPolicies.IsInAppRole(context.User, DocumentFlowApp.Core.Security.AppRoles.Employee)));
    });

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(DatabaseConfig.GetConnectionString());
        options.EnableDetailedErrors();
    });
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();

    var app = builder.Build();
    AppendTrace("App built");

    try
    {
        await ApplicationDbSeeder.SeedAsync(app.Services);
        AppendTrace("Database seeded");
    }
    catch (Exception ex)
    {
        AppendCrashLog("Database seeding failed", ex.ToString());
        throw;
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    app.Use(async (context, next) =>
    {
        AppendTrace($"REQ {context.Request.Method} {context.Request.Path}");
        await next();
        AppendTrace($"RES {context.Response.StatusCode} {context.Request.Method} {context.Request.Path}");
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    AppendTrace("App run");
    app.Run();
}
catch (Exception ex)
{
    AppendCrashLog("Fatal startup exception", ex.ToString());
    throw;
}
