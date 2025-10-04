using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotebookAI.Triples.Files;

namespace NotebookAI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStore _files;

    public FilesController(IFileStore files) => _files = files;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string prefix = "", CancellationToken ct = default)
    {
        var list = await _files.ListAsync(prefix, ct);
        return Ok(list);
    }

    [HttpGet("raw")] // download
    public async Task<IActionResult> Get([FromQuery] string path, CancellationToken ct)
    {
        var entry = await _files.GetAsync(path, ct);
        if (entry == null) return NotFound();
        // Non-Azure triple store implementation does not store stream retrieval yet (placeholder)
        return Ok(entry);
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload([FromQuery] string path, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("Empty file");
        await using var stream = file.OpenReadStream();
        var entry = await _files.UpsertAsync(path, stream, file.ContentType, ct);
        return Ok(entry);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string path, CancellationToken ct)
    {
        var ok = await _files.DeleteAsync(path, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}
