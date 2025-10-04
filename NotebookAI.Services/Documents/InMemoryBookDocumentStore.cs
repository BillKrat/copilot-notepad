using Adventures.Shared.Documents;

namespace NotebookAI.Services.Documents;

public sealed class InMemoryBookDocumentStore : InMemoryDocumentStore<BookDocument>, IBookDocumentStore
{
}
