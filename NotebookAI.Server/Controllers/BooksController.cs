using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotebookAI.Triples.Config;
using NotebookAI.Triples.TripleStore;

namespace NotebookAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BooksController : ControllerBase
{
    private readonly IBookConfigProvider _provider;
    private readonly ITripleStore _triples;
    private readonly IBookConfigCacheInvalidator? _cacheInvalidator;

    public BooksController(IBookConfigProvider provider, ITripleStore triples, IServiceProvider sp)
    {
        _provider = provider; _triples = triples; _cacheInvalidator = sp.GetService<IBookConfigCacheInvalidator>();
    }


    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var books = await _provider.GetBooksAsync(ct);
        return Ok(books.Select(b => new { b.Id, b.Title, b.PublicationDate }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var b = await _provider.GetBookAsync(id, ct);
        if (b == null) return NotFound();
        return Ok(b);
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        var count = await _triples.CountAsync(ct);
        if (count == 0)
        {
            foreach (var (s,p,o,d,dt) in NotebookAI.Triples.Ontology.BookOntologySeed.Triples)
            {
                await _triples.CreateAsync(s,p,o,d,dt,ct);
            }
            _cacheInvalidator?.Invalidate();
            return Ok(new { Seeded = true, Triples = await _triples.CountAsync(ct) });
        }
        return Ok(new { Seeded = false, Triples = count });
    }

    [HttpPost("invalidate-cache")] 
    public IActionResult Invalidate([FromQuery] string? id = null)
    {
        _cacheInvalidator?.Invalidate(id);
        return Ok(new { Invalidated = id ?? "ALL" });
    }

    [HttpGet("{id}/chapters")] 
    public async Task<IActionResult> GetChapters(string id, CancellationToken ct)
    {
        var triples = await _triples.QueryAsync(subject: id, predicate: "hasChapter", ct: ct);
        var chapters = triples.Select(t => t.Object).Where(o => o != null).Distinct().ToList();
        return Ok(chapters);
    }

    [HttpGet("chapters/{chapterId}/paragraphs")] 
    public async Task<IActionResult> GetParagraphs(string chapterId, CancellationToken ct)
    {
        var triples = await _triples.QueryAsync(subject: chapterId, predicate: "hasParagraph", ct: ct);
        var paragraphs = triples.Select(t => t.Object).Where(o => o != null).Distinct().ToList();
        return Ok(paragraphs);
    }

    [HttpGet("paragraphs/{paragraphId}/sentences")] 
    public async Task<IActionResult> GetSentences(string paragraphId, CancellationToken ct)
    {
        var triples = await _triples.QueryAsync(subject: paragraphId, predicate: "hasSentence", ct: ct);
        var sentences = triples.Select(t => t.Object).Where(o => o != null).Distinct().ToList();
        return Ok(sentences);
    }

    [HttpGet("paragraphs/{paragraphId}/content")] 
    public async Task<IActionResult> GetParagraphContent(string paragraphId, CancellationToken ct)
    {
        // Content stored as (paragraphId, hasContent, null, Data=content)
        var triples = await _triples.QueryAsync(subject: paragraphId, predicate: "hasContent", ct: ct);
        var content = triples.Select(t => t.GraphContext).Where(d => !string.IsNullOrEmpty(d)).FirstOrDefault();
        return Ok(new { paragraphId, content });
    }

    [HttpGet("sentences/{sentenceId}")] 
    public async Task<IActionResult> GetSentence(string sentenceId, CancellationToken ct)
    {
        // Sentence text stored as (sentenceId, hasText, null, Data=sentence)
        var triples = await _triples.QueryAsync(subject: sentenceId, predicate: "hasText", ct: ct);
        var text = triples.Select(t => t.GraphContext).Where(d => !string.IsNullOrEmpty(d)).FirstOrDefault();
        if (text == null) return NotFound();
        return Ok(new { sentenceId, text });
    }
}
