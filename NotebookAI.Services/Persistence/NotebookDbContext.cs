using Microsoft.EntityFrameworkCore;
using NotebookAI.Services.Documents;

namespace NotebookAI.Services.Persistence;

public class NotebookDbContext : DbContext
{
    public NotebookDbContext(DbContextOptions<NotebookDbContext> options) : base(options) {}

    public DbSet<BookDocumentEntity> BookDocuments => Set<BookDocumentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<BookDocumentEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired().HasMaxLength(512);
            b.Property(x => x.Author).IsRequired().HasMaxLength(256);
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.Property(x => x.TagsJson).HasColumnName("Tags");
        });
    }
}

public class BookDocumentEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? TagsJson { get; set; }
}
