using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotebookAI.Services.Interfaces;
using NotebookAI.Services.Models;

namespace NotebookAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SaintsController : ControllerBase
{
    private readonly ISaintsDocumentStore _store;
    private readonly SaintsRagService _rag;
    private readonly ILogger<SaintsController> _logger;

    public SaintsController(ISaintsDocumentStore store, SaintsRagService rag, ILogger<SaintsController> logger)
    {
        _store = store;
        _rag = rag;
        _logger = logger;
    }

    [HttpPost("documents")] // Add or update a document
    public async Task<IActionResult> UpsertDocument([FromBody] SaintDocumentInput input, CancellationToken ct)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Content))
            return BadRequest("Invalid document input");
        var doc = new SaintDocument(input.Id, input.Title ?? input.Id, input.Author ?? "Unknown", input.Date ?? DateTime.UtcNow.Date, input.Content, input.Tags);
        await _store.AddOrUpdateAsync(doc, ct);
        return Ok();
    }

    [HttpGet("documents")] // List docs
    public async Task<IActionResult> ListDocuments(CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        return Ok(all.Select(d => new { d.Id, d.Title, d.Author, d.Date }));
    }

    [HttpPost("ask")] // RAG question
    public async Task<IActionResult> Ask([FromBody] AskRequest req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Question required");
        var answer = await _rag.AskAsync(req.Question, req.Top ?? 4, ct);
        return Ok(new { answer });
    }

    public sealed class SaintDocumentInput
    {
        public string Id { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Author { get; set; }
        public DateTime? Date { get; set; }
        public string Content { get; set; } = string.Empty;
        public Dictionary<string,string>? Tags { get; set; }
    }

    public sealed class AskRequest
    {
        public string Question { get; set; } = string.Empty;
        public int? Top { get; set; }
    }
}
