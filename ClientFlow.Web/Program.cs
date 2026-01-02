using ClientFlow.Application.Abstractions;
using ClientFlow.Application.Services;
using ClientFlow.Application.Surveys.Validation;
using ClientFlow.Infrastructure;
using ClientFlow.Infrastructure.Repositories;
using ClientFlow.Infrastructure.Schema;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DB
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") ??
    builder.Configuration.GetConnectionString("Default") ??
    builder.Configuration["ConnectionStrings:DefaultConnection"] ??
    builder.Configuration["ConnectionStrings:Default"] ??
    throw new InvalidOperationException(
        "A SQL Server connection string named 'DefaultConnection' or 'Default' must be configured.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));

// DI
builder.Services.AddScoped<ISurveyRepository, SurveyRepository>();
builder.Services.AddScoped<IResponseRepository, ResponseRepository>();
builder.Services.AddScoped<IOptionRepository, OptionRepository>();
builder.Services.AddScoped<IRuleRepository, RuleRepository>();
builder.Services.AddScoped<ISurveyVersionRepository, SurveyVersionRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddScoped<ISectionRepository, SectionRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddSingleton<IQuestionSettingsValidator, JsonSchemaQuestionSettingsValidator>();

// Email and reporting
builder.Services.Configure<ClientFlow.Infrastructure.Email.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddSingleton<ClientFlow.Infrastructure.Email.EmailService>();
builder.Services.AddHostedService<ClientFlow.Infrastructure.Background.DailyReportService>();

// JWT settings and authentication/authorization
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<AuthService>();
// Configure JWT bearer authentication.  Tokens are validated against the configured issuer,
// audience and secret key.  The clock skew is kept small to reduce the window for expired
// tokens to be accepted.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    // Default policies are sufficient because we rely on role names stored in the token.
    // Additional policies could be defined here if needed.
});


// Add controllers with JSON options: case-insensitive properties and enum strings.
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Accept camelCase or lowercase JSON property names.
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Serialize/deserialize enums as strings and allow matching from strings.
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Persist data protection keys to a known directory so the application can start even
// when running under different user profiles (such as IIS application pool identities).
// Protect the keys to the local machine to avoid DPAPI decrypt failures when the user
// profile changes.
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .ProtectKeysWithDpapi(protectToLocalMachine: true)
    .SetApplicationName("ClientFlow");

var app = builder.Build();

// Ensure the database schema is up to date before handling requests.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DatabaseSchemaFixer.EnsureMustChangePasswordColumn(db);
    DatabaseSchemaFixer.EnsureCreatedByUserIdColumn(db);
    DatabaseSchemaFixer.EnsurePasswordResetTokensTable(db);
    DatabaseSchemaFixer.EnsureLegacySurveySeed(db);
    DatabaseSchemaFixer.EnsureLegacyKioskFeedbackColumns(db);
    DatabaseSchemaFixer.EnsureKioskFeedbackTimingDefaults(db);
}

app.UseSwagger();
app.UseSwaggerUI();

// Authentication and authorisation must be added before mapping controllers to ensure
// endpoints decorated with [Authorize] are protected.
app.UseAuthentication();
app.UseAuthorization();

// serve static UI from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
