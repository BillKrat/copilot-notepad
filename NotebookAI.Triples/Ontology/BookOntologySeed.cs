namespace NotebookAI.Triples.Ontology;

/// <summary>
/// Provides a minimal book ontology-inspired seed (loosely referencing schema.org / Dublin Core concepts) as triples.
/// This is a pragmatic starter, not a full OWL/RDF import.
/// </summary>
public static class BookOntologySeed
{
    public static (string subject, string? predicate, string? obj, string? data, string? dataType)[] Triples =>
        new (string subject, string? predicate, string? obj, string? data, string? dataType)[]
        {
            ("Book", "rdf:type", "rdfs:Class", null, null), 
            ("Author", "rdf:type", "rdfs:Class", null, null), 
            ("writes", "rdf:type", "rdf:Property", null, null), 
            ("writes", "rdfs:domain", "Author", null, null), 
            ("writes", "rdfs:range", "Book", null, null), 
            ("hasTitle", "rdf:type", "rdf:Property", null, null), 
            ("hasTitle", "rdfs:domain", "Book", null, null), 
            ("hasTitle", "rdfs:range", "xsd:string", null, null), 
            ("hasPublicationDate", "rdf:type", "rdf:Property", null, null), 
            ("hasPublicationDate", "rdfs:domain", "Book", null, null), 
            ("hasPublicationDate", "rdfs:range", "xsd:date", null, null), 

            // Example instance data
            ("author:tolkien", "rdf:type", "Author", null, null),
            ("author:tolkien", "foaf:name", "J.R.R. Tolkien", null, null), 
            ("book:lotr", "rdf:type", "Book", null, null), 
            ("book:lotr", "hasTitle", "The Lord of the Rings", null, null), 
            ("book:lotr", "hasPublicationDate", "1954-07-29", null, "xsd:date"), 
            ("author:tolkien", "writes", "book:lotr", null, null),
            
            // Authors
            ("author:alice", "rdf:type", "Author", null, null),
            ("author:alice", "foaf:name", "Alice Ipsum", null, null),

            ("author:bob", "rdf:type", "Author", null, null),
            ("author:bob", "foaf:name", "Bob Lorem", null, null),

            // Books
            ("book:lorem101", "rdf:type", "Book", null, null),
            ("book:lorem101", "hasTitle", "Lorem Ipsum 101", null, null),
            ("book:lorem101", "hasPublicationDate", "2023-01-15", null, "xsd:date"),
            ("author:alice", "writes", "book:lorem101", null, null),

            ("book:ipsumInsights", "rdf:type", "Book", null, null),
            ("book:ipsumInsights", "hasTitle", "Ipsum Insights", null, null),
            ("book:ipsumInsights", "hasPublicationDate", "2024-06-30", null, "xsd:date"),
            ("author:bob", "writes", "book:ipsumInsights", null, null),

            // Chapters
            ("chapter:lorem101c1", "rdf:type", "Chapter", null, null),
            ("book:lorem101", "hasChapter", "chapter:lorem101c1", null, null),

            ("chapter:ipsumInsightsc1", "rdf:type", "Chapter", null, null),
            ("book:ipsumInsights", "hasChapter", "chapter:ipsumInsightsc1", null, null),

            // Paragraphs
            ("paragraph:lorem101c1p1", "rdf:type", "Paragraph", null, null),
            ("chapter:lorem101c1", "hasParagraph", "paragraph:lorem101c1p1", null, null),
            ("paragraph:lorem101c1p1", "hasContent", null, "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.", "xsd:string"),

            ("paragraph:ipsumInsightsc1p1", "rdf:type", "Paragraph", null, null),
            ("chapter:ipsumInsightsc1", "hasParagraph", "paragraph:ipsumInsightsc1p1", null, null),
            ("paragraph:ipsumInsightsc1p1", "hasContent", null, "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", "xsd:string"),

            // Sentences
            ("sentence:lorem101c1p1s1", "rdf:type", "Sentence", null, null),
            ("sentence:lorem101c1p1s1", "hasText", null, "Lorem ipsum dolor sit amet, consectetur adipiscing elit.", "xsd:string"),
            ("paragraph:lorem101c1p1", "hasSentence", "sentence:lorem101c1p1s1", null, null),

            ("sentence:lorem101c1p1s2", "rdf:type", "Sentence", null, null),
            ("sentence:lorem101c1p1s2", "hasText", null, "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.", "xsd:string"),
            ("paragraph:lorem101c1p1", "hasSentence", "sentence:lorem101c1p1s2", null, null),

            ("sentence:ipsumInsightsc1p1s1", "rdf:type", "Sentence", null, null),
            ("sentence:ipsumInsightsc1p1s1", "hasText", null, "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", "xsd:string"),
            ("paragraph:ipsumInsightsc1p1", "hasSentence", "sentence:ipsumInsightsc1p1s1", null, null)
        };
}



