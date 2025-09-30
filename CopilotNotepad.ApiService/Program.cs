using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CopilotNotepad.ApiService.Data;
using CopilotNotepad.ApiService.Models;
using CopilotNotepad.ApiService.Services;
using CopilotNotepad.ApiService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:4201", "https://localhost:4201")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Entity Framework
builder.Services.AddDbContext<NotesDbContext>(options =>
    options.UseInMemoryDatabase("NotesDb"));

// Add Auth0 JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth0:Domain"];
        options.Audience = builder.Configuration["Auth0:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// Add AI service (mock for now, will be replaced with OpenAI integration)
builder.Services.AddScoped<IAiService, MockAiService>();

// Add structured logging
builder.Logging.AddJsonConsole();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();

// Health checks endpoint
app.MapHealthChecks("/health");

// Helper method to validate request and get user ID
static string? GetUserId(ClaimsPrincipal user)
{
    return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

static bool ValidateRequest(object request, out List<string> errors)
{
    errors = new List<string>();
    var validationContext = new ValidationContext(request);
    var validationResults = new List<ValidationResult>();
    
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        errors = validationResults.Select(vr => vr.ErrorMessage ?? "Validation error").ToList();
        return false;
    }
    return true;
}

// Notes API endpoints with improved error handling and validation
app.MapGet("/api/notes", async (NotesDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        logger.LogInformation("Fetching notes for user {UserId}", userId);

        var notes = await db.Notes
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();
        
        return Results.Ok(notes);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching notes");
        throw;
    }
})
.RequireAuthorization()
.WithName("GetNotes")
.WithTags("Notes");

app.MapGet("/api/notes/{id}", async (int id, NotesDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        logger.LogInformation("Fetching note {NoteId} for user {UserId}", id, userId);

        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        return note is not null ? Results.Ok(note) : Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching note {NoteId}", id);
        throw;
    }
})
.RequireAuthorization()
.WithName("GetNote")
.WithTags("Notes");

app.MapPost("/api/notes", async (CreateNoteRequest request, NotesDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (!ValidateRequest(request, out var errors))
            return Results.BadRequest(new { errors });

        logger.LogInformation("Creating note for user {UserId}", userId);

        var note = new Note
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync();

        logger.LogInformation("Created note {NoteId} for user {UserId}", note.Id, userId);
        return Results.Created($"/api/notes/{note.Id}", note);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating note");
        throw;
    }
})
.RequireAuthorization()
.WithName("CreateNote")
.WithTags("Notes");

app.MapPut("/api/notes/{id}", async (int id, UpdateNoteRequest request, NotesDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (!ValidateRequest(request, out var errors))
            return Results.BadRequest(new { errors });

        logger.LogInformation("Updating note {NoteId} for user {UserId}", id, userId);

        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note is null)
            return Results.NotFound();

        note.Title = request.Title.Trim();
        note.Content = request.Content.Trim();
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Updated note {NoteId} for user {UserId}", id, userId);
        return Results.Ok(note);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating note {NoteId}", id);
        throw;
    }
})
.RequireAuthorization()
.WithName("UpdateNote")
.WithTags("Notes");

app.MapDelete("/api/notes/{id}", async (int id, NotesDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        logger.LogInformation("Deleting note {NoteId} for user {UserId}", id, userId);

        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note is null)
            return Results.NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted note {NoteId} for user {UserId}", id, userId);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting note {NoteId}", id);
        throw;
    }
})
.RequireAuthorization()
.WithName("DeleteNote")
.WithTags("Notes");

// Future AI endpoints (placeholder for OpenAI integration)
app.MapPost("/api/ai/enhance", async (string content, IAiService aiService, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var enhancedContent = await aiService.EnhanceContentAsync(content);
        return Results.Ok(new { enhancedContent });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error enhancing content");
        throw;
    }
})
.RequireAuthorization()
.WithName("EnhanceContent")
.WithTags("AI");

app.MapDefaultEndpoints();

app.Run();