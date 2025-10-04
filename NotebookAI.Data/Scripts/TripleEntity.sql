CREATE TABLE TripleEntity (
    Id TEXT PRIMARY KEY,
    Subject TEXT,
    Predicate TEXT,
    Object TEXT,
    Data TEXT,
    DataType TEXT,
    UpdatedUtc TEXT,
    CreatedUtc TEXT    
);

-- Indexes for triple pattern queries
CREATE INDEX IX_TripleEntity_Subject_Predicate_Object ON TripleEntity(Subject, Predicate, Object);
CREATE INDEX IX_TripleEntity_Predicate_Object ON TripleEntity(Predicate, Object);
CREATE INDEX IX_TripleEntity_Object_Predicate ON TripleEntity(Object, Predicate);
CREATE INDEX IX_TripleEntity_Subject ON TripleEntity(Subject);
CREATE INDEX IX_TripleEntity_Predicate ON TripleEntity(Predicate);
CREATE INDEX IX_TripleEntity_Object ON TripleEntity(Object);