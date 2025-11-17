using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using McpNetDll.Registry;
using McpNetDll.Repository;
using Directory = Lucene.Net.Store.Directory;

namespace McpNetDll.Core.Indexing
{
    /// <summary>
    /// Lucene.NET-based implementation of the indexing service for fast keyword searching
    /// </summary>
    public class LuceneIndexingService : IIndexingService, IDisposable
    {
        private readonly ITypeRegistry _typeRegistry;
        private readonly object _indexLock = new object();

        private Directory? _indexDirectory;
        private Analyzer? _analyzer;
        private IndexWriter? _indexWriter;
        private DirectoryReader? _directoryReader;
        private IndexSearcher? _indexSearcher;

        private DateTime _lastBuildTime;
        private long _indexSizeEstimate;
        private bool _isDisposed;

        // Field names
        private const string FIELD_ID = "id";
        private const string FIELD_ELEMENT_TYPE = "element_type";
        private const string FIELD_NAME = "name";
        private const string FIELD_NAME_EXACT = "name_exact";
        private const string FIELD_FULL_NAME = "full_name";
        private const string FIELD_PARENT_TYPE = "parent_type";
        private const string FIELD_RETURN_TYPE = "return_type";
        private const string FIELD_DOCUMENTATION = "documentation";
        private const string FIELD_CONTENT = "content"; // Combined searchable field
        private const string FIELD_NAMESPACE = "namespace";
        private const string FIELD_PARAMETERS = "parameters";

        // Use LuceneVersion.LUCENE_48 for compatibility
        private static readonly LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

        public LuceneIndexingService(ITypeRegistry typeRegistry)
        {
            _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
            InitializeIndex();
        }

        private void InitializeIndex()
        {
            // Use RAM directory for in-memory indexing (fast but volatile)
            // For persistence, use FSDirectory.Open(new DirectoryInfo("path/to/index"))
            _indexDirectory = new RAMDirectory();

            // Create a custom analyzer that handles code-specific tokenization
            _analyzer = CreateCodeAnalyzer();

            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            _indexWriter = new IndexWriter(_indexDirectory, indexConfig);
        }

        private Analyzer CreateCodeAnalyzer()
        {
            // Create a custom analyzer for code
            // This handles camelCase, PascalCase, snake_case, etc.
            var analyzerMap = new Dictionary<string, Analyzer>
            {
                // Use KeywordAnalyzer for exact matching fields
                { FIELD_NAME_EXACT, new KeywordAnalyzer() },
                { FIELD_ELEMENT_TYPE, new KeywordAnalyzer() },
                { FIELD_FULL_NAME, new KeywordAnalyzer() },

                // Use a more sophisticated analyzer for searchable content
                { FIELD_CONTENT, new StandardAnalyzer(LUCENE_VERSION) },
                { FIELD_DOCUMENTATION, new EnglishAnalyzer(LUCENE_VERSION) },

                // Use WhitespaceAnalyzer for name fields to preserve casing but split on spaces
                { FIELD_NAME, new WhitespaceAnalyzer(LUCENE_VERSION) },
                { FIELD_NAMESPACE, new WhitespaceAnalyzer(LUCENE_VERSION) },
                { FIELD_PARAMETERS, new WhitespaceAnalyzer(LUCENE_VERSION) }
            };

            return new PerFieldAnalyzerWrapper(
                new StandardAnalyzer(LUCENE_VERSION),
                analyzerMap
            );
        }

        public void BuildIndex()
        {
            lock (_indexLock)
            {
                var sw = Stopwatch.StartNew();

                // Clear existing index
                _indexWriter?.DeleteAll();
                _indexWriter?.Commit();

                // Get all types from registry
                var allTypes = _typeRegistry.GetAllTypes();

                int documentCount = 0;

                // Index each type and its members
                foreach (var type in allTypes)
                {
                    documentCount += IndexType(type);
                }

                // Commit changes and optimize
                _indexWriter?.Commit();
                _indexWriter?.Flush(true, true);

                // Refresh the searcher
                RefreshSearcher();

                _lastBuildTime = DateTime.UtcNow;
                _indexSizeEstimate = EstimateIndexSize();

                sw.Stop();
                Console.WriteLine($"Lucene index built in {sw.ElapsedMilliseconds}ms with {documentCount} documents");
            }
        }

        private int IndexType(TypeMetadata type)
        {
            int documentCount = 0;
            // Handle nested types properly - they already have the Parent+NestedType format in their Name
            var typeFullName = string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";

            // Index the type itself
            var typeDoc = new Document();

            // Stored fields (retrievable)
            typeDoc.Add(new StringField(FIELD_ID, $"type:{typeFullName}", Field.Store.YES));
            typeDoc.Add(new StringField(FIELD_ELEMENT_TYPE, "Type", Field.Store.YES));
            typeDoc.Add(new TextField(FIELD_NAME, type.Name, Field.Store.YES));
            typeDoc.Add(new StringField(FIELD_NAME_EXACT, type.Name.ToLowerInvariant(), Field.Store.NO));
            typeDoc.Add(new StringField(FIELD_FULL_NAME, typeFullName, Field.Store.YES));

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                typeDoc.Add(new TextField(FIELD_NAMESPACE, type.Namespace, Field.Store.YES));
            }

            if (!string.IsNullOrEmpty(type.Documentation))
            {
                typeDoc.Add(new TextField(FIELD_DOCUMENTATION, type.Documentation, Field.Store.YES));
            }

            // Combined content field for searching (tokenized)
            var content = BuildSearchableContent(type.Name, typeFullName, type.Namespace, type.Documentation);
            typeDoc.Add(new TextField(FIELD_CONTENT, content, Field.Store.NO));

            _indexWriter?.AddDocument(typeDoc);
            documentCount++;

            // Index methods
            if (type.Methods != null)
            {
                foreach (var method in type.Methods)
                {
                    var methodDoc = new Document();

                    methodDoc.Add(new StringField(FIELD_ID, $"method:{typeFullName}.{method.Name}", Field.Store.YES));
                    methodDoc.Add(new StringField(FIELD_ELEMENT_TYPE, "Method", Field.Store.YES));
                    methodDoc.Add(new TextField(FIELD_NAME, method.Name, Field.Store.YES));
                    methodDoc.Add(new StringField(FIELD_NAME_EXACT, method.Name.ToLowerInvariant(), Field.Store.NO));
                    methodDoc.Add(new StringField(FIELD_FULL_NAME, $"{typeFullName}.{method.Name}", Field.Store.YES));
                    methodDoc.Add(new StringField(FIELD_PARENT_TYPE, typeFullName, Field.Store.YES));

                    if (!string.IsNullOrEmpty(method.ReturnType))
                    {
                        methodDoc.Add(new TextField(FIELD_RETURN_TYPE, method.ReturnType, Field.Store.YES));
                    }

                    if (!string.IsNullOrEmpty(method.Documentation))
                    {
                        methodDoc.Add(new TextField(FIELD_DOCUMENTATION, method.Documentation, Field.Store.YES));
                    }

                    // Add parameters
                    if (method.Parameters != null && method.Parameters.Any())
                    {
                        var paramStr = string.Join(" ", method.Parameters.Select(p => $"{p.Name} {p.Type}"));
                        methodDoc.Add(new TextField(FIELD_PARAMETERS, paramStr, Field.Store.YES));
                    }

                    // Combined content
                    var paramText = method.Parameters != null
                        ? string.Join(" ", method.Parameters.Select(p => $"{p.Name} {p.Type}"))
                        : null;
                    var methodContent = BuildSearchableContent(
                        method.Name,
                        $"{typeFullName}.{method.Name}",
                        type.Namespace,
                        method.Documentation,
                        method.ReturnType,
                        paramText
                    );
                    methodDoc.Add(new TextField(FIELD_CONTENT, methodContent, Field.Store.NO));

                    _indexWriter?.AddDocument(methodDoc);
                    documentCount++;
                }
            }

            // Index properties
            if (type.Properties != null)
            {
                foreach (var property in type.Properties)
                {
                    var propDoc = new Document();

                    propDoc.Add(new StringField(FIELD_ID, $"property:{typeFullName}.{property.Name}", Field.Store.YES));
                    propDoc.Add(new StringField(FIELD_ELEMENT_TYPE, "Property", Field.Store.YES));
                    propDoc.Add(new TextField(FIELD_NAME, property.Name, Field.Store.YES));
                    propDoc.Add(new StringField(FIELD_NAME_EXACT, property.Name.ToLowerInvariant(), Field.Store.NO));
                    propDoc.Add(new StringField(FIELD_FULL_NAME, $"{typeFullName}.{property.Name}", Field.Store.YES));
                    propDoc.Add(new StringField(FIELD_PARENT_TYPE, typeFullName, Field.Store.YES));

                    if (!string.IsNullOrEmpty(property.Type))
                    {
                        propDoc.Add(new TextField(FIELD_RETURN_TYPE, property.Type, Field.Store.YES));
                    }

                    if (!string.IsNullOrEmpty(property.Documentation))
                    {
                        propDoc.Add(new TextField(FIELD_DOCUMENTATION, property.Documentation, Field.Store.YES));
                    }

                    var propContent = BuildSearchableContent(
                        property.Name,
                        $"{typeFullName}.{property.Name}",
                        type.Namespace,
                        property.Documentation,
                        property.Type
                    );
                    propDoc.Add(new TextField(FIELD_CONTENT, propContent, Field.Store.NO));

                    _indexWriter?.AddDocument(propDoc);
                    documentCount++;
                }
            }

            // Index fields
            if (type.Fields != null)
            {
                foreach (var field in type.Fields)
                {
                    var fieldDoc = new Document();

                    fieldDoc.Add(new StringField(FIELD_ID, $"field:{typeFullName}.{field.Name}", Field.Store.YES));
                    fieldDoc.Add(new StringField(FIELD_ELEMENT_TYPE, "Field", Field.Store.YES));
                    fieldDoc.Add(new TextField(FIELD_NAME, field.Name, Field.Store.YES));
                    fieldDoc.Add(new StringField(FIELD_NAME_EXACT, field.Name.ToLowerInvariant(), Field.Store.NO));
                    fieldDoc.Add(new StringField(FIELD_FULL_NAME, $"{typeFullName}.{field.Name}", Field.Store.YES));
                    fieldDoc.Add(new StringField(FIELD_PARENT_TYPE, typeFullName, Field.Store.YES));

                    if (!string.IsNullOrEmpty(field.Type))
                    {
                        fieldDoc.Add(new TextField(FIELD_RETURN_TYPE, field.Type, Field.Store.YES));
                    }

                    if (!string.IsNullOrEmpty(field.Documentation))
                    {
                        fieldDoc.Add(new TextField(FIELD_DOCUMENTATION, field.Documentation, Field.Store.YES));
                    }

                    var fieldContent = BuildSearchableContent(
                        field.Name,
                        $"{typeFullName}.{field.Name}",
                        type.Namespace,
                        field.Documentation,
                        field.Type
                    );
                    fieldDoc.Add(new TextField(FIELD_CONTENT, fieldContent, Field.Store.NO));

                    _indexWriter?.AddDocument(fieldDoc);
                    documentCount++;
                }
            }

            // Index enum values
            if (type.EnumValues != null)
            {
                foreach (var enumValue in type.EnumValues)
                {
                    var enumDoc = new Document();

                    enumDoc.Add(new StringField(FIELD_ID, $"enum:{typeFullName}.{enumValue.Name}", Field.Store.YES));
                    enumDoc.Add(new StringField(FIELD_ELEMENT_TYPE, "EnumValue", Field.Store.YES));
                    enumDoc.Add(new TextField(FIELD_NAME, enumValue.Name, Field.Store.YES));
                    enumDoc.Add(new StringField(FIELD_NAME_EXACT, enumValue.Name.ToLowerInvariant(), Field.Store.NO));
                    enumDoc.Add(new StringField(FIELD_FULL_NAME, $"{typeFullName}.{enumValue.Name}", Field.Store.YES));
                    enumDoc.Add(new StringField(FIELD_PARENT_TYPE, typeFullName, Field.Store.YES));

                    if (!string.IsNullOrEmpty(enumValue.Value))
                    {
                        enumDoc.Add(new TextField(FIELD_RETURN_TYPE, enumValue.Value, Field.Store.YES));
                    }

                    var enumContent = BuildSearchableContent(
                        enumValue.Name,
                        $"{typeFullName}.{enumValue.Name}",
                        type.Namespace,
                        null,
                        enumValue.Value
                    );
                    enumDoc.Add(new TextField(FIELD_CONTENT, enumContent, Field.Store.NO));

                    _indexWriter?.AddDocument(enumDoc);
                    documentCount++;
                }
            }

            return documentCount;
        }

        private string BuildSearchableContent(params string?[] parts)
        {
            var contentParts = new List<string>();

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    // Handle nested type notation (Parent+NestedType)
                    if (part.Contains('+'))
                    {
                        var nestedParts = part.Split('+');
                        foreach (var nestedPart in nestedParts)
                        {
                            contentParts.Add(nestedPart); // Add each part separately
                            contentParts.AddRange(SplitCamelCase(nestedPart)); // Tokenize each part
                        }
                        // Also add the full name without the +
                        contentParts.Add(part.Replace('+', ' '));
                    }
                    else
                    {
                        // Split camelCase and PascalCase
                        var tokens = SplitCamelCase(part);
                        contentParts.Add(part); // Original
                        contentParts.AddRange(tokens); // Tokenized
                    }
                }
            }

            return string.Join(" ", contentParts);
        }

        private List<string> SplitCamelCase(string text)
        {
            var tokens = new List<string>();
            var currentToken = "";

            foreach (char c in text)
            {
                if (char.IsUpper(c) && currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToLowerInvariant());
                    currentToken = c.ToString();
                }
                else if (char.IsLetterOrDigit(c))
                {
                    currentToken += c;
                }
                else if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToLowerInvariant());
                    currentToken = "";
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToLowerInvariant());
            }

            return tokens;
        }

        public KeywordSearchResult SearchByKeywords(string keywords, string searchScope = "all", int limit = 100, int offset = 0)
        {
            var sw = Stopwatch.StartNew();

            lock (_indexLock)
            {
                RefreshSearcher();

                if (_indexSearcher == null)
                {
                    return new KeywordSearchResult(
                        new List<KeywordSearchHit>(),
                        new PaginationInfo { Total = 0, Limit = limit, Offset = offset },
                        keywords,
                        0,
                        new Dictionary<string, int>()
                    );
                }

                try
                {
                    // Build the query
                    Query query = BuildSearchQuery(keywords, searchScope);

                    // Execute search
                    var topDocs = _indexSearcher.Search(query, offset + limit);
                    var totalHits = topDocs.TotalHits;

                    // Process results
                    var hits = new List<KeywordSearchHit>();
                    var facetCounts = new Dictionary<string, int>();

                    // Calculate facets from all results
                    for (int i = 0; i < Math.Min(topDocs.ScoreDocs.Length, 1000); i++)
                    {
                        var doc = _indexSearcher.Doc(topDocs.ScoreDocs[i].Doc);
                        var elementType = doc.Get(FIELD_ELEMENT_TYPE);
                        facetCounts[elementType] = facetCounts.GetValueOrDefault(elementType, 0) + 1;
                    }

                    // Get paginated results
                    for (int i = offset; i < Math.Min(topDocs.ScoreDocs.Length, offset + limit); i++)
                    {
                        var scoreDoc = topDocs.ScoreDocs[i];
                        var doc = _indexSearcher.Doc(scoreDoc.Doc);

                        var hit = ConvertToSearchHit(doc, scoreDoc.Score, keywords);
                        hits.Add(hit);
                    }

                    sw.Stop();

                    return new KeywordSearchResult(
                        hits,
                        new PaginationInfo { Total = totalHits, Limit = limit, Offset = offset },
                        keywords,
                        sw.Elapsed.TotalMilliseconds,
                        facetCounts
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Search error: {ex.Message}");
                    return new KeywordSearchResult(
                        new List<KeywordSearchHit>(),
                        new PaginationInfo { Total = 0, Limit = limit, Offset = offset },
                        keywords,
                        sw.Elapsed.TotalMilliseconds,
                        new Dictionary<string, int>()
                    );
                }
            }
        }

        private Query BuildSearchQuery(string keywords, string searchScope)
        {
            var booleanQuery = new BooleanQuery();

            // Handle nested type searches - replace + with space for better matching
            var searchText = keywords.Replace('+', ' ');
            var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length == 0)
            {
                return booleanQuery; // Empty query
            }

            // Build query for each term across multiple fields
            foreach (var term in terms)
            {
                var termLower = term.ToLowerInvariant();
                var termBoolQuery = new BooleanQuery();

                // Search in multiple fields
                termBoolQuery.Add(new WildcardQuery(new Term(FIELD_NAME, $"*{termLower}*")), Occur.SHOULD);
                termBoolQuery.Add(new WildcardQuery(new Term(FIELD_CONTENT, $"*{termLower}*")), Occur.SHOULD);
                termBoolQuery.Add(new WildcardQuery(new Term(FIELD_DOCUMENTATION, $"*{termLower}*")), Occur.SHOULD);
                termBoolQuery.Add(new WildcardQuery(new Term(FIELD_PARAMETERS, $"*{termLower}*")), Occur.SHOULD);

                // All terms must match somewhere
                booleanQuery.Add(termBoolQuery, Occur.MUST);
            }

            // Add scope filter
            if (searchScope != "all")
            {
                var scopeQuery = searchScope.ToLowerInvariant() switch
                {
                    "types" => new TermQuery(new Term(FIELD_ELEMENT_TYPE, "Type")),
                    "methods" => new TermQuery(new Term(FIELD_ELEMENT_TYPE, "Method")),
                    "properties" => new TermQuery(new Term(FIELD_ELEMENT_TYPE, "Property")),
                    "fields" => new TermQuery(new Term(FIELD_ELEMENT_TYPE, "Field")),
                    "enums" => new TermQuery(new Term(FIELD_ELEMENT_TYPE, "EnumValue")),
                    _ => null
                };

                if (scopeQuery != null)
                {
                    booleanQuery.Add(scopeQuery, Occur.MUST);
                }
            }

            return booleanQuery;
        }

        private KeywordSearchHit ConvertToSearchHit(Document doc, float score, string keywords)
        {
            var elementType = doc.Get(FIELD_ELEMENT_TYPE) ?? "";
            var name = doc.Get(FIELD_NAME) ?? "";
            var fullName = doc.Get(FIELD_FULL_NAME) ?? "";
            var parentType = doc.Get(FIELD_PARENT_TYPE);
            var returnType = doc.Get(FIELD_RETURN_TYPE);
            var documentation = doc.Get(FIELD_DOCUMENTATION);

            // Extract matched terms and highlight positions
            var matchedTerms = new List<string>();
            var highlights = new Dictionary<string, List<int>>();

            var searchTerms = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in searchTerms)
            {
                var lowerTerm = term.ToLowerInvariant();

                // Check if term matches in name
                if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedTerms.Add(term);
                    var positions = FindAllPositions(name, term);
                    if (positions.Any())
                    {
                        highlights[$"name:{term}"] = positions;
                    }
                }

                // Check if term matches in documentation
                if (!string.IsNullOrEmpty(documentation) &&
                    documentation.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!matchedTerms.Contains(term))
                        matchedTerms.Add(term);

                    var positions = FindAllPositions(documentation, term);
                    if (positions.Any())
                    {
                        highlights[$"doc:{term}"] = positions;
                    }
                }
            }

            return new KeywordSearchHit(
                ElementType: elementType,
                Name: name,
                FullName: fullName,
                ParentType: parentType,
                ReturnType: returnType,
                Documentation: documentation,
                RelevanceScore: Math.Round(score, 2),
                MatchedTerms: matchedTerms,
                HighlightPositions: highlights
            );
        }

        private List<int> FindAllPositions(string text, string searchTerm)
        {
            var positions = new List<int>();
            int index = 0;

            while ((index = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                positions.Add(index);
                index += searchTerm.Length;
            }

            return positions;
        }

        private void RefreshSearcher()
        {
            try
            {
                _directoryReader?.Dispose();
                _directoryReader = DirectoryReader.Open(_indexDirectory);
                _indexSearcher = new IndexSearcher(_directoryReader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh searcher: {ex.Message}");
                _indexSearcher = null;
            }
        }

        public void UpdateIndex()
        {
            // For simplicity, rebuild the entire index
            BuildIndex();
        }

        public void ClearIndex()
        {
            lock (_indexLock)
            {
                _indexWriter?.DeleteAll();
                _indexWriter?.Commit();
                _indexSizeEstimate = 0;
            }
        }

        public IndexStatistics GetStatistics()
        {
            lock (_indexLock)
            {
                RefreshSearcher();

                if (_directoryReader == null)
                {
                    return new IndexStatistics(0, 0, 0, 0, 0, 0, 0, 0, _lastBuildTime.ToString("O"));
                }

                var docCount = _directoryReader.NumDocs;
                var termCount = 0;

                // Count documents by type
                var typeCounts = new Dictionary<string, int>();
                for (int i = 0; i < docCount; i++)
                {
                    try
                    {
                        var doc = _directoryReader.Document(i);
                        var elementType = doc.Get(FIELD_ELEMENT_TYPE);
                        if (!string.IsNullOrEmpty(elementType))
                        {
                            typeCounts[elementType] = typeCounts.GetValueOrDefault(elementType, 0) + 1;
                        }
                    }
                    catch
                    {
                        // Document might be deleted
                    }
                }

                // Estimate term count based on document count
                // (In Lucene.Net 4.8, the Fields API is different)
                termCount = docCount * 10; // Rough estimate: 10 unique terms per document

                return new IndexStatistics(
                    TotalDocuments: docCount,
                    TotalTerms: termCount,
                    TypesIndexed: typeCounts.GetValueOrDefault("Type", 0),
                    MethodsIndexed: typeCounts.GetValueOrDefault("Method", 0),
                    PropertiesIndexed: typeCounts.GetValueOrDefault("Property", 0),
                    FieldsIndexed: typeCounts.GetValueOrDefault("Field", 0),
                    EnumsIndexed: typeCounts.GetValueOrDefault("EnumValue", 0),
                    IndexSizeBytes: _indexSizeEstimate,
                    LastBuildTime: _lastBuildTime.ToString("O")
                );
            }
        }

        private long EstimateIndexSize()
        {
            try
            {
                if (_indexDirectory is RAMDirectory ramDir)
                {
                    // For RAM directory, estimate based on file sizes
                    long totalSize = 0;
                    foreach (var fileName in ramDir.ListAll())
                    {
                        totalSize += ramDir.FileLength(fileName);
                    }
                    return totalSize;
                }
                else if (_indexDirectory is FSDirectory fsDir)
                {
                    // For file system directory, calculate actual disk usage
                    var dirInfo = new DirectoryInfo(fsDir.Directory.FullName);
                    return dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                }
            }
            catch
            {
                // Fallback estimation
            }

            return _directoryReader?.NumDocs * 1024 ?? 0; // Rough estimate: 1KB per document
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_indexLock)
            {
                _indexSearcher = null;
                _directoryReader?.Dispose();
                _indexWriter?.Dispose();
                _analyzer?.Dispose();
                _indexDirectory?.Dispose();

                _isDisposed = true;
            }
        }
    }
}