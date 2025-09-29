using System.ComponentModel.DataAnnotations;

namespace CopilotNotepad.ApiService.Models;

public class CreateNoteRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
}