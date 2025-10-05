using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NotebookAI.Triples.Ontology;
using NotebookAI.Triples.TripleStore;

namespace NotebookAI.Triples.Tests;

[TestClass]
public class TripleStoreCrudTests
{
    private IDbContextFactory<TripleDbContext> _factory = default!;
    private ITripleStore _store = default!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TripleDbContext>()
            .UseSqlite("Data Source=C:\\Data\\github-repos\\copilot-ai\\NotebookAI.Server\\NotepadTripleStore.db") // "Data Source=:memory:")
            .Options;

        // For in-memory sqlite we must open connection & ensure created
        var ctx = new TripleDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        ctx.Dispose();

        _factory = new PooledDbContextFactory<TripleDbContext>(options);
        _store = new EfTripleStore(_factory);
    }

    [TestMethod]
    public async Task Create_Read_Update_Delete_List()
    {
        var created = await _store.CreateAsync("book:1", "hasTitle", "Test Book", data: "{\"pages\":123}");
        Assert.IsNotNull(created.Id);

        var fetched = await _store.GetByIdAsync(created.Id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("Test Book", fetched!.Object);

        var updatedOk = await _store.UpdateAsync(created.Id, @object: "Updated Book", data: "{\"pages\":150}");
        Assert.IsTrue(updatedOk);
        var updated = await _store.GetByIdAsync(created.Id);
        Assert.AreEqual("Updated Book", updated!.Object);

        var list = await _store.QueryAsync(subject: "book:1");
        Assert.AreEqual(1, list.Count);

        var deleted = await _store.DeleteAsync(created.Id);
        Assert.IsTrue(deleted);
        var afterDelete = await _store.GetByIdAsync(created.Id);
        Assert.IsNull(afterDelete);
    }

    [TestMethod]
    public async Task Seed_Ontology_And_Query()
    {
        foreach (var (s,p,o,data,dt) in BookOntologySeed.Triples)
        {
            await _store.CreateAsync(s,p,o,data,dt);
        }
        var count = await _store.CountAsync();
        Assert.IsTrue(count >= BookOntologySeed.Triples.Length);

        var lotrLinks = await _store.QueryAsync(subject: "book:lotr");
        Assert.IsTrue(lotrLinks.Any(t => t.Predicate == "hasTitle"));

        var authorBooks = await _store.QueryAsync(subject: "author:tolkien");
        Assert.IsTrue(authorBooks.Any(t => t.Predicate == "writes"));
    }
}
