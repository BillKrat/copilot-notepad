using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using CopilotNotepad.ApiService.Data;
using CopilotNotepad.ApiService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();

// Notes API endpoints
app.MapGet("/api/notes", async (NotesDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var notes = await db.Notes
        .Where(n => n.UserId == userId)
        .OrderByDescending(n => n.UpdatedAt)
        .ToListAsync();
    
    return Results.Ok(notes);
})
.RequireAuthorization();

app.MapGet("/api/notes/{id}", async (int id, NotesDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    return note is not null ? Results.Ok(note) : Results.NotFound();
})
.RequireAuthorization();

app.MapPost("/api/notes", async (CreateNoteRequest request, NotesDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var note = new Note
    {
        Title = request.Title,
        Content = request.Content,
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Notes.Add(note);
    await db.SaveChangesAsync();

    return Results.Created($"/api/notes/{note.Id}", note);
})
.RequireAuthorization();

app.MapPut("/api/notes/{id}", async (int id, UpdateNoteRequest request, NotesDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null)
        return Results.NotFound();

    note.Title = request.Title;
    note.Content = request.Content;
    note.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(note);
})
.RequireAuthorization();

app.MapDelete("/api/notes/{id}", async (int id, NotesDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    if (note is null)
        return Results.NotFound();

    db.Notes.Remove(note);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();
