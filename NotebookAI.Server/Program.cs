using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Options;
using NotebookAI.Server.Settings;
using NotebookAI.Server.Services;

namespace NotebookAI.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Bind AI settings
        builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));

        // Get Auth0 configuration from user secrets / appsettings
        var auth0Domain = builder.Configuration["Auth0:Domain"];
        var auth0ClientId = builder.Configuration["Auth0:ClientId"];
        var auth0Audience = builder.Configuration["Auth0:Audience"];

        // Validate required configuration
        if (string.IsNullOrEmpty(auth0Domain) || string.IsNullOrEmpty(auth0ClientId) || string.IsNullOrEmpty(auth0Audience))
        {
            throw new InvalidOperationException("Auth0 configuration is missing. Please check your user secrets or appsettings.json");
        }

        // Add services to the container.
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo 
            { 
                Title = "NotebookAI API", 
                Version = "v1",
                Description = "API for NotebookAI application with Auth0 authentication"
            });

            // Add OAuth2 for automated authentication - using configuration
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

            // Keep your existing Bearer token for manual input
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Auto-populated after OAuth2 auth.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            // Require OAuth2 (Bearer will be auto-populated)
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

        // Configure JWT Authentication for Auth0 - using configuration
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

        // Add Authorization policies (unchanged)
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy;
        });

        // CORS configuration (unchanged)
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin)) return false;
                    
                    var uri = new Uri(origin);
                    var host = uri.Host;
                    
                    if (host == "localhost" || host == "127.0.0.1")
                        return true;
                    
                    return host == "global-webnet.com" || host.EndsWith(".global-webnet.com");
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });
        });

        // Register in-memory Saints document store & RAG service
        builder.Services.AddSingleton<ISaintsDocumentStore, InMemorySaintsDocumentStore>();
        builder.Services.AddSingleton<SaintsRagService>();

        // Register Semantic Kernel (dual provider) + embeddings
        builder.Services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var ai = sp.GetRequiredService<IOptions<AiSettings>>().Value;

            var kb = Kernel.CreateBuilder();
            kb.Services.AddLogging();

            var provider = ai.Provider?.Trim() ?? "OpenAI";
            var chatModel = ai.ChatModelId ?? "gpt-4o-mini";
            var embeddingModel = ai.EmbeddingModelId ?? "text-embedding-3-small";

            if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = ai.AzureEndpoint ?? config["AZURE_OPENAI_ENDPOINT"];
                var apiKey = ai.AzureApiKey ?? config["AZURE_OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                var deployment = ai.AzureChatDeployment ?? "gpt-4o";
                var embeddingDeployment = ai.AzureEmbeddingDeployment ?? "text-embedding-3-small";
                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("Azure OpenAI configuration missing Endpoint or ApiKey");
                }
                kb.AddAzureOpenAIChatCompletion(
                    deploymentName: deployment,
                    endpoint: endpoint,
                    apiKey: apiKey);
                kb.AddAzureOpenAITextEmbeddingGeneration(
                    deploymentName: embeddingDeployment,
                    endpoint: endpoint,
                    apiKey: apiKey);
            }
            else
            {
                var apiKey = ai.OpenAIApiKey ?? config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI ApiKey not configured");
                }
                kb.AddOpenAIChatCompletion(
                    modelId: chatModel,
                    apiKey: apiKey);
                kb.AddOpenAITextEmbeddingGeneration(
                    modelId: embeddingModel,
                    apiKey: apiKey);
            }
            return kb.Build();
        });

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
                
                // OAuth configuration for Auth0 - using configuration
                c.OAuthClientId(auth0ClientId);
                c.OAuthAppName("NotebookAI API Swagger");
                c.OAuthUsePkce();
                
                c.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
                {
                    { "audience", auth0Audience }
                });

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
