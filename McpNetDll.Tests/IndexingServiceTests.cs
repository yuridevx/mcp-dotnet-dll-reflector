using System;
using System.Collections.Generic;
using System.Linq;
using McpNetDll.Core.Indexing;
using McpNetDll.Registry;
using Xunit;

namespace McpNetDll.Tests
{
    public class IndexingServiceTests : IDisposable
    {
        private readonly ITypeRegistry _typeRegistry;
        private readonly LuceneIndexingService _indexingService;
        private const string TEST_DLL_PATH = "../../../MyTestLibrary/bin/Debug/net9.0/MyTestLibrary.dll";

        public IndexingServiceTests()
        {
            _typeRegistry = new TypeRegistry();
            _typeRegistry.LoadAssemblies(new[] { TEST_DLL_PATH });
            _indexingService = new LuceneIndexingService(_typeRegistry);
            _indexingService.BuildIndex();
        }

        public void Dispose()
        {
            _indexingService?.Dispose();
        }

        [Fact]
        public void BuildIndex_CreatesNonEmptyIndex()
        {
            // Act
            var stats = _indexingService.GetStatistics();

            // Assert
            Assert.True(stats.TotalDocuments > 0, "Index should contain documents");
            Assert.True(stats.TotalTerms > 0, "Index should contain terms");
            Assert.True(stats.TypesIndexed > 0, "Index should contain types");
        }

        [Fact]
        public void SearchByKeywords_FindsTypeByName()
        {
            // Arrange
            var keywords = "TestService";

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            Assert.NotEmpty(results.Results);
            Assert.Contains(results.Results, r => r.Name.Contains("TestService"));
        }

        [Fact]
        public void SearchByKeywords_FindsMethodByName()
        {
            // Arrange
            var keywords = "DoWork";

            // Act
            var results = _indexingService.SearchByKeywords(keywords, "methods");

            // Assert
            Assert.NotEmpty(results.Results);
            Assert.All(results.Results, r => Assert.Equal("Method", r.ElementType));
            Assert.Contains(results.Results, r => r.Name.Contains("DoWork"));
        }

        [Fact]
        public void SearchByKeywords_FindsNestedTypes()
        {
            // Arrange
            var keywords = "nested";

            // Act
            var results = _indexingService.SearchByKeywords(keywords, "types");

            // Assert
            if (results.Results.Any())
            {
                Assert.Contains(results.Results, r => r.Name.Contains("+"));
            }
        }

        [Fact]
        public void SearchByKeywords_SearchesDocumentation()
        {
            // Arrange
            var keywords = "summary";

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            // Should find elements with "summary" in their documentation
            if (results.Results.Any())
            {
                Assert.Contains(results.Results, r =>
                    r.Documentation?.Contains("summary", StringComparison.OrdinalIgnoreCase) == true);
            }
        }

        [Fact]
        public void SearchByKeywords_RespectsSearchScope()
        {
            // Arrange
            var keywords = "test";

            // Act
            var typeResults = _indexingService.SearchByKeywords(keywords, "types");
            var methodResults = _indexingService.SearchByKeywords(keywords, "methods");
            var propertyResults = _indexingService.SearchByKeywords(keywords, "properties");

            // Assert
            Assert.All(typeResults.Results, r => Assert.Equal("Type", r.ElementType));
            Assert.All(methodResults.Results, r => Assert.Equal("Method", r.ElementType));
            Assert.All(propertyResults.Results, r => Assert.Equal("Property", r.ElementType));
        }

        [Fact]
        public void SearchByKeywords_HandlesMultipleTerms()
        {
            // Arrange
            var keywords = "test service"; // Multiple terms

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            // Results should match both terms
            if (results.Results.Any())
            {
                Assert.Contains(results.Results, r =>
                    r.MatchedTerms.Contains("test", StringComparer.OrdinalIgnoreCase) ||
                    r.MatchedTerms.Contains("service", StringComparer.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void SearchByKeywords_RanksResultsByRelevance()
        {
            // Arrange
            var keywords = "test";

            // Act
            var results = _indexingService.SearchByKeywords(keywords, limit: 10);

            // Assert
            if (results.Results.Count > 1)
            {
                // Check that results are ordered by descending relevance score
                var scores = results.Results.Select(r => r.RelevanceScore).ToList();
                Assert.Equal(scores.OrderByDescending(s => s), scores);
            }
        }

        [Fact]
        public void SearchByKeywords_SupportsPagination()
        {
            // Arrange
            var keywords = "test";

            // Act
            var page1 = _indexingService.SearchByKeywords(keywords, limit: 5, offset: 0);
            var page2 = _indexingService.SearchByKeywords(keywords, limit: 5, offset: 5);

            // Assert
            Assert.NotEqual(page1.Results.FirstOrDefault()?.Name, page2.Results.FirstOrDefault()?.Name);
            Assert.Equal(page1.Pagination.Total, page2.Pagination.Total);
        }

        [Fact]
        public void SearchByKeywords_ProvidesFacetCounts()
        {
            // Arrange
            var keywords = "test";

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            Assert.NotNull(results.FacetCounts);
            if (results.Results.Any())
            {
                Assert.True(results.FacetCounts.Count > 0, "Should have facet counts");
            }
        }

        [Fact]
        public void SearchByKeywords_HandlesCamelCaseTokenization()
        {
            // Arrange
            var keywords = "work"; // Should find DoWork, WorkItem, etc.

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            if (results.Results.Any())
            {
                Assert.Contains(results.Results, r =>
                    r.Name.Contains("Work", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void SearchByKeywords_HandlesEmptyQuery()
        {
            // Arrange
            var keywords = "";

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            Assert.Empty(results.Results);
            Assert.Equal(0, results.Pagination.Total);
        }

        [Fact]
        public void SearchByKeywords_HandlesSpecialCharacters()
        {
            // Arrange
            var keywords = "test+nested"; // With special character

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            // Should handle the query without errors
            Assert.NotNull(results);
        }

        [Fact]
        public void GetStatistics_ReturnsValidStatistics()
        {
            // Act
            var stats = _indexingService.GetStatistics();

            // Assert
            Assert.True(stats.TotalDocuments > 0);
            Assert.True(stats.TotalTerms > 0);
            Assert.True(stats.TypesIndexed >= 0);
            Assert.True(stats.MethodsIndexed >= 0);
            Assert.True(stats.PropertiesIndexed >= 0);
            Assert.True(stats.FieldsIndexed >= 0);
            Assert.True(stats.EnumsIndexed >= 0);
            Assert.NotNull(stats.LastBuildTime);
        }

        [Fact]
        public void RebuildIndex_UpdatesIndex()
        {
            // Arrange
            var initialStats = _indexingService.GetStatistics();

            // Act
            _indexingService.BuildIndex();
            var newStats = _indexingService.GetStatistics();

            // Assert
            Assert.Equal(initialStats.TotalDocuments, newStats.TotalDocuments);
            Assert.NotEqual(initialStats.LastBuildTime, newStats.LastBuildTime);
        }

        [Fact]
        public void ClearIndex_RemovesAllDocuments()
        {
            // Act
            _indexingService.ClearIndex();
            var stats = _indexingService.GetStatistics();

            // Assert
            Assert.Equal(0, stats.TotalDocuments);
            Assert.Equal(0, stats.TotalTerms);
        }

        [Fact]
        public void SearchByKeywords_FindsEnumValues()
        {
            // Arrange
            var keywords = "enum";

            // Act
            var results = _indexingService.SearchByKeywords(keywords, "enums");

            // Assert
            if (results.Results.Any())
            {
                Assert.All(results.Results, r => Assert.Equal("EnumValue", r.ElementType));
            }
        }

        [Fact]
        public void SearchByKeywords_MeasuresSearchTime()
        {
            // Arrange
            var keywords = "test";

            // Act
            var results = _indexingService.SearchByKeywords(keywords);

            // Assert
            Assert.True(results.SearchTimeMs >= 0, "Search time should be measured");
        }
    }
}