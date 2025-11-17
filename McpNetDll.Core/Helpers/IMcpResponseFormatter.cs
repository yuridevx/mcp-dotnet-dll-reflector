using McpNetDll.Core.Indexing;
using McpNetDll.Registry;
using McpNetDll.Repository;

namespace McpNetDll.Helpers;

public interface IMcpResponseFormatter
{
    string FormatNamespaceResponse(NamespaceQueryResult result, ITypeRegistry registry);
    string FormatTypeDetailsResponse(TypeDetailsQueryResult result, ITypeRegistry registry);
    string FormatSearchResponse(SearchQueryResult result, ITypeRegistry registry);
    string FormatKeywordSearchResponse(KeywordSearchResult result, ITypeRegistry registry);
}