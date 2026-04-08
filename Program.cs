using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenAI; // ✅ Official SDK
using SaaSForge.Api.Configurations;
using SaaSForge.Api.Data;
using SaaSForge.Api.Infrastructure.BackgroundWorkers;
using SaaSForge.Api.Models;
using SaaSForge.Api.Models.Auth;
using SaaSForge.Api.Modules.Leads.Interfaces;
using SaaSForge.Api.Modules.Leads.Services;
using SaaSForge.Api.Services.Ai;
using SaaSForge.Api.Services.Auth;
using SaaSForge.Api.Services.Business;
using SaaSForge.Api.Services.Common;
using SaaSForge.Api.Services.Interfaces;
using SaaSForge.Api.Services.Subscription;
using SaaSForge.Api.Services.Usage;
using System.ClientModel;
using System.Text;
// using OpenAI.Chat; // (If you reference typed Chat client elsewhere)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ✅ Configure logging level globally
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ---------------------------
// 1) Database
// ---------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------------------
// 2) Identity
// ---------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});

// ---------------------------
// 3) JWT Auth
// ---------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.Zero
    };
});

// ---------------------------
// 4) CORS (Expo / Web / Mobile)
// ---------------------------
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll",
//        x => x
//        .AllowAnyHeader()
//        .AllowAnyMethod()
//        .AllowCredentials()
//        .SetIsOriginAllowed(_ => true));   // TEMPORARY FOR DEV
//});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                return origin == "http://localhost:3000"
                    || origin == "https://leadflow-ai-nine.vercel.app";
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------
// 5) Controllers + Swagger
// ---------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LeadFlow AI API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",

        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ---------------------------
// 6) App Services
// ---------------------------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IUsageService, UsageService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentActivationService, PaymentActivationService>();
builder.Services.AddScoped<GoogleTokenValidatorService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<ILeadAiService, LeadAiService>();
builder.Services.AddScoped<ILeadTagService, LeadTagService>();
builder.Services.AddScoped<ILeadActivityService, LeadActivityService>();
builder.Services.AddScoped<ILeadFollowUpIntelligenceService, LeadFollowUpIntelligenceService>();
builder.Services.AddScoped<ILeadAttentionIntelligenceService, LeadAttentionIntelligenceService>();
builder.Services.AddScoped<ILeadAlertService, LeadAlertService>();
builder.Services.AddHostedService<LeadAlertsWorker>();
builder.Services.AddScoped<ILeadAiScenarioResolver, LeadAiScenarioResolver>();
builder.Services.AddScoped<ILeadAiPromptBuilder, LeadAiPromptBuilder>();
builder.Services.AddScoped<ILeadAiSuggestionService, LeadAiSuggestionService>();
builder.Services.AddHttpClient<IOpenAiService, OpenAiService>(client =>
{
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["OpenAI:ApiKey"]}");
});

builder.Services.Configure<ResendSettings>(
    builder.Configuration.GetSection("Resend"));

builder.Services.AddHttpClient<IEmailService, EmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// ✅ Bind ExpoPush from appsettings.json using ONE options class (Configurations)
builder.Services.Configure<ExpoPushOptions>(
    builder.Configuration.GetSection("ExpoPush")
);

builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<ClientAppSettings>(
    builder.Configuration.GetSection("ClientApp"));

builder.Services.Configure<PasswordResetSettings>(
    builder.Configuration.GetSection("PasswordReset"));

builder.Services.Configure<EmailConfirmationSettings>(
    builder.Configuration.GetSection("EmailConfirmation"));

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // Prefer environment variable in production (Render)
    var apiKey =
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? config["OpenAI:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Missing OpenAI API key. Set OPENAI_API_KEY env var or OpenAI:ApiKey config.");

    var opts = new OpenAIClientOptions();
    return new OpenAIClient(new ApiKeyCredential(apiKey), opts);
});

// You may also want IHttpClientFactory generally:
builder.Services.AddHttpClient();


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------
// 9) App Pipeline
// ---------------------------
var app = builder.Build();

var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
app.Logger.LogInformation("OPENAI_API_KEY present: {Present}, length: {Len}",
    !string.IsNullOrWhiteSpace(envKey),
    string.IsNullOrWhiteSpace(envKey) ? 0 : envKey.Length);

//var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
//app.Urls.Add($"http://0.0.0.0:{port}");

var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

static async System.Threading.Tasks.Task SeedRolesAndAdminUserAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // ---- Ensure Roles Exist ----
    var roles = new[] { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ---- Create Default Admin ----
    var adminEmail = "kgvishnupandit@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser is null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "LeadFlowAI Admin"
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@123");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    return; // 👈 Fix CS0161
}

static void SeedSubscriptionPlans(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.SubscriptionPlans.Any(x => x.Code == "free"))
    {
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Code = "free",
            Name = "Free",
            MonthlyAiRequestLimit = 50,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    if (!db.SubscriptionPlans.Any(x => x.Code == "pro"))
    {
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Code = "pro",
            Name = "Pro",
            MonthlyAiRequestLimit = 1000,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    db.SaveChanges();
}

// Trust X-Forwarded-* (Azure/AppGW/NGINX)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LeadFlow AI API v1");
    c.RoutePrefix = "swagger";
});

app.UseForwardedHeaders();
//app.UseHttpsRedirection();

app.UseRouting();

//app.UseCors("AllowAll");   // between UseRouting and Auth
app.UseCors("AllowFrontend");   // between UseRouting and Auth

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Simple health root
app.MapGet("/", () => Results.Ok("✅ LeadFlow AI API running"));

// ⬇️ ADD THIS BEFORE app.Run()
await SeedRolesAndAdminUserAsync(app);
SeedSubscriptionPlans(app);

// ---------------------------
// 10) Run
// ---------------------------
app.Run();
