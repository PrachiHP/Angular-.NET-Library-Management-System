using System.Text;
using System.Text.Json.Serialization;
using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Middleware;
using BackendAPI.Repositories;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services (the DI container)
// ---------------------------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialise AND accept enums as their names ("WaterDamage") rather than
        // the default numeric ordinal. Without this, clients must send 2 instead
        // of "WaterDamage", and any reordering of the enum silently changes meaning.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// [ApiController] auto-validation returns RFC 9110 ProblemDetails by default,
// a different shape from our ErrorResponse. Override it so the Angular client
// only ever has one error contract to handle.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ErrorResponse(
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            errors));
    };
});

builder.Services.AddEndpointsApiExplorer();

// Swagger needs to be told about bearer auth, or the "Authorize" button and
// the Authorization header on try-it-out requests will not appear.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Library API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token only — Swagger adds the 'Bearer ' prefix.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// Scoped: one AppDbContext per request. It is not thread-safe and tracks
// changes, so a singleton would leak state between users.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Options pattern: bind config sections to typed classes, validated once at
// startup rather than re-read as loose strings all over the codebase.
builder.Services.Configure<LoanOptions>(builder.Configuration.GetSection(LoanOptions.SectionName));
builder.Services.Configure<FineOptions>(builder.Configuration.GetSection(FineOptions.SectionName));

// Needed by CurrentUser to read claims off the active request.
builder.Services.AddHttpContextAccessor();

// Repositories. The open generic covers IRepository<Category>, IRepository<Author>
// and every other entity with no per-type registration.
builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IIssueRepository, IssueRepository>();

// Services registered against their INTERFACE, so swapping an implementation
// is one line here and no change to any controller.
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<IssueNotificationHandlers>();
builder.Services.AddScoped<IEngagementService, EngagementService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ITrendingService, TrendingService>();

// Strategy via DI: swap for an AzureBlobFileStorageService and nothing else changes.
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();

// Long-running background worker, replacing Hangfire. Registered as a singleton
// by AddHostedService, which is why it takes IServiceScopeFactory internally.
builder.Services.AddHostedService<DueDateReminderService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---- Authentication ----
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Every one of these is a real check. Turning any of them off in
        // production weakens the token: an expired or foreign-issued token
        // would then be accepted.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateLifetime = true,
            // Default is 5 minutes of leeway on expiry; zero makes tokens
            // expire exactly when they say they do.
            ClockSkew = TimeSpan.Zero,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline — order is significant, each wraps everything after it
// ---------------------------------------------------------------------------

// First, so it catches exceptions thrown by everything below.
app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

// Must run before the endpoints it protects.
// Serves wwwroot, including uploaded cover and quote images.
app.UseStaticFiles();

app.UseCors("AllowAngular");

// Authentication BEFORE authorization: "who are you?" must be answered before
// "are you allowed?". Reversed, every [Authorize] would fail.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DataSeeder.SeedAsync(app.Services);

app.Run();
