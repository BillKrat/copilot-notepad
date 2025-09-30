using System.ComponentModel.DataAnnotations;

namespace CopilotNotepad.ApiService.Models;

public class CreateNoteRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Content is required")]
    [StringLength(10000, ErrorMessage = "Content cannot exceed 10,000 characters")]
    public string Content { get; set; } = string.Empty;
}