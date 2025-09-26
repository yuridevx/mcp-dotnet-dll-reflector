namespace McpNetDll.Helpers;

public interface IMcpResponseFormatter
{
    string FormatNamespaceResponse(Repository.NamespaceQueryResult result, Registry.ITypeRegistry registry);
    string FormatTypeDetailsResponse(Repository.TypeDetailsQueryResult result, Registry.ITypeRegistry registry);
    string FormatSearchResponse(Repository.SearchQueryResult result, Registry.ITypeRegistry registry);
}

