using Adventures.Shared.AI;
using Adventures.Shared.Extensions;
using Adventures.Shared.Documents;
using Adventures.Shared.Rag;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotebookAI.Services.Documents;
using NotebookAI.Services.Rag;
using System.Security.Claims;
using NotebookAI.Services.Persistence; // Added for persistence factory
using NotebookAI.Triples.TripleStore; // triple store
using NotebookAI.Triples.Config; // book config provider
using NotebookAI.Triples.Files; // file store
using Microsoft.EntityFrameworkCore; // added for EnsureCreated

namespace NotebookAI.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Centralized defaults (remove if obsolete in future)
        builder.AddServiceDefaults();

        builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));

        var auth0Domain = builder.Configuration["Auth0:Domain"];
        var auth0ClientId = builder.Configuration["Auth0:ClientId"];
        var auth0Audience = builder.Configuration["Auth0:Audience"];
        if (string.IsNullOrEmpty(auth0Domain) || string.IsNullOrEmpty(auth0ClientId) || string.IsNullOrEmpty(auth0Audience))
        {
            throw new InvalidOperationException("Auth0 configuration is missing. Please check your user secrets or appsettings.json");
        }

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NotebookAI API",
                Version = "v1",
                Description = "API for NotebookAI application with Auth0 authentication"
            });
            c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"https://{auth0Domain}/authorize"),
                        TokenUrl = new Uri($"https://{auth0Domain}/oauth/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "openid", "OpenID Connect scope" },
                            { "profile", "Profile information" },
                            { "email", "Email address" }
                        }
                    }
                }
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Auto-populated after OAuth2 auth.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "OAuth2"
                        }
                    },
                    new[] { "openid", "profile", "email" }
                }
            });
        });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0Domain}/";
                options.Audience = auth0Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.NameIdentifier
                };
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            });

        builder.Services.AddAuthorization(opts =>
        {
            opts.FallbackPolicy = opts.DefaultPolicy;
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin)) return false;
                    var uri = new Uri(origin);
                    var host = uri.Host;
                    if (host == "localhost" || host == "127.0.0.1") return true;
                    return host == "global-webnet.com" || host.EndsWith(".global-webnet.com");
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });
        });

        // Persistence (EF Core) - replaces in-memory document store
        builder.Services.AddNotebookPersistence(builder.Configuration);

        // Triple store (book/blog metadata, ontology)
        builder.Services.AddTripleStore(builder.Configuration);
        builder.Services.AddBookConfigProvider(builder.Configuration);
        builder.Services.AddFileStore(builder.Configuration);

        // Seed triple store if empty (ontology + sample data)
        builder.Services.AddHostedService<TripleStoreSeeder>();

        // Document & RAG wiring
        builder.Services.AddSingleton(typeof(IDocumentStore<>), typeof(InMemoryDocumentStore<>));
        builder.Services.AddSingleton<IChunker<BookDocument, BookChunk>, ParagraphChunker>();
        builder.Services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();
        builder.Services.AddSingleton<IAdvancedRagService, HybridRagService>();

        // Refactored AI kernel registration
        builder.Services.AddAiKernel(builder.Configuration);

        var app = builder.Build();

        app.MapDefaultEndpoints();
        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotebookAI API V1");
                c.DocumentTitle = "NotebookAI API Documentation";
                c.OAuthClientId(auth0ClientId);
                c.OAuthAppName("NotebookAI API Swagger");
                c.OAuthUsePkce();
                c.OAuthAdditionalQueryStringParams(new Dictionary<string, string> { { "audience", auth0Audience } });
                c.InjectJavascript("/swagger-auth-helper.js");
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapFallbackToFile("/index.html");
        app.Run();
    }
}

