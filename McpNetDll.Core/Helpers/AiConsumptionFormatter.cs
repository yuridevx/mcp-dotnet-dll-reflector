using System.Text;
using McpNetDll.Core.Indexing;
using McpNetDll.Registry;
using McpNetDll.Repository;

namespace McpNetDll.Helpers;

/// <summary>
/// Concise formatter optimized for AI consumption with minimal token usage.
/// Renders C# code in a readable but compact format.
/// </summary>
public class AiConsumptionFormatter : IMcpResponseFormatter
{
    public string FormatNamespaceResponse(NamespaceQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
            return $"ERROR: {result.Error}";

        var sb = new StringBuilder();
        sb.AppendLine($"// {result.Pagination.Total} namespaces found");

        foreach (var ns in result.Namespaces)
        {
            sb.AppendLine($"\nnamespace {ns.Name} // {ns.TypeCount} types");
            if (ns.Types?.Any() == true)
            {
                foreach (var type in ns.Types.Take(5)) // Show first 5 types as preview
                {
                    sb.AppendLine($"  {GetTypeSignature(type)}");
                }
                if (ns.Types.Count > 5)
                    sb.AppendLine($"  // ... +{ns.Types.Count - 5} more");
            }
        }

        if (result.Pagination.Total > result.Namespaces.Count)
            sb.AppendLine($"\n// Showing {result.Namespaces.Count}/{result.Pagination.Total} (offset: {result.Pagination.Offset})");

        return sb.ToString().TrimEnd();
    }

    public string FormatTypeDetailsResponse(TypeDetailsQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            var sb = new StringBuilder($"ERROR: {result.Error}");
            if (result.AvailableTypes?.Any() == true)
            {
                sb.AppendLine("\n// Did you mean:");
                foreach (var type in result.AvailableTypes.Take(10))
                    sb.AppendLine($"//   {type}");
            }
            return sb.ToString();
        }

        var output = new StringBuilder();

        foreach (var type in result.Types)
        {
            RenderTypeDetails(output, type);
            if (type != result.Types.Last())
                output.AppendLine("\n");
        }

        return output.ToString().TrimEnd();
    }

    public string FormatSearchResponse(SearchQueryResult result, ITypeRegistry registry)
    {
        if (!string.IsNullOrEmpty(result.Error))
            return $"ERROR: {result.Error}";

        var sb = new StringBuilder();
        sb.AppendLine($"// {result.Pagination.Total} matches found");

        var groupedResults = result.Results.GroupBy(r => r.ElementType);

        foreach (var group in groupedResults)
        {
            sb.AppendLine($"\n// {group.Key} ({group.Count()}):");
            foreach (var item in group)
            {
                sb.AppendLine($"{item.FullName ?? item.Name}");
            }
        }

        if (result.Pagination.Total > result.Results.Count)
            sb.AppendLine($"\n// Showing {result.Results.Count}/{result.Pagination.Total} (offset: {result.Pagination.Offset})");

        return sb.ToString().TrimEnd();
    }

    public string FormatKeywordSearchResponse(KeywordSearchResult result, ITypeRegistry registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Keyword search: \"{result.SearchTerms}\"");
        sb.AppendLine($"// {result.Pagination.Total} matches found in {result.SearchTimeMs:F1}ms");

        if (result.FacetCounts?.Any() == true)
        {
            var facets = string.Join(", ", result.FacetCounts.Select(f => $"{f.Key}: {f.Value}"));
            sb.AppendLine($"// Distribution: {facets}");
        }

        if (!result.Results.Any())
        {
            sb.AppendLine("// No matches found");
            return sb.ToString().TrimEnd();
        }

        var groupedResults = result.Results.GroupBy(r => r.ElementType);

        foreach (var group in groupedResults)
        {
            sb.AppendLine($"\n// {group.Key} ({group.Count()}):");
            foreach (var item in group)
            {
                var score = item.RelevanceScore > 0 ? $" [score: {item.RelevanceScore}]" : "";
                var matched = item.MatchedTerms?.Any() == true ? $" (matched: {string.Join(", ", item.MatchedTerms)})" : "";
                sb.AppendLine($"{item.FullName ?? item.Name}{score}{matched}");

                if (!string.IsNullOrWhiteSpace(item.Documentation))
                {
                    var docPreview = item.Documentation.Length > 100
                        ? item.Documentation.Substring(0, 97) + "..."
                        : item.Documentation;
                    sb.AppendLine($"  // {docPreview.Replace("\n", " ")}");
                }
            }
        }

        if (result.Pagination.Total > result.Results.Count)
            sb.AppendLine($"\n// Showing {result.Results.Count}/{result.Pagination.Total} (offset: {result.Pagination.Offset})");

        return sb.ToString().TrimEnd();
    }

    private void RenderTypeDetails(StringBuilder sb, TypeMetadata type)
    {
        // Add XML documentation if available
        if (!string.IsNullOrEmpty(type.Documentation))
        {
            foreach (var line in type.Documentation.Split('\n'))
                sb.AppendLine($"/// {line.Trim()}");
        }

        // Render type declaration
        sb.AppendLine($"namespace {type.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public {type.TypeKind.ToLower()} {type.Name}");
        sb.AppendLine("{");

        // Render enum values
        if (type.EnumValues?.Any() == true)
        {
            foreach (var enumValue in type.EnumValues)
            {
                if (!string.IsNullOrEmpty(enumValue.Value))
                    sb.AppendLine($"    {enumValue.Name} = {enumValue.Value},");
                else
                    sb.AppendLine($"    {enumValue.Name},");
            }
        }

        // Render fields (including struct layout if applicable)
        if (type.Fields?.Any() == true)
        {
            if (type.StructLayout != null)
            {
                sb.AppendLine($"    // Layout: {type.StructLayout.Kind}, Pack: {type.StructLayout.Pack}, Size: {type.StructLayout.Size}");
            }

            foreach (var field in type.Fields)
            {
                RenderField(sb, field);
            }

            if (type.Properties?.Any() == true || type.Methods?.Any() == true)
                sb.AppendLine();
        }

        // Render properties
        if (type.Properties?.Any() == true)
        {
            foreach (var prop in type.Properties)
            {
                RenderProperty(sb, prop);
            }

            if (type.Methods?.Any() == true)
                sb.AppendLine();
        }

        // Render methods
        if (type.Methods?.Any() == true)
        {
            foreach (var method in type.Methods)
            {
                RenderMethod(sb, method);
                if (method != type.Methods.Last())
                    sb.AppendLine();
            }
        }

        sb.AppendLine("}");
    }

    private void RenderField(StringBuilder sb, FieldMetadata field)
    {
        if (!string.IsNullOrEmpty(field.Documentation))
            sb.AppendLine($"    /// {TruncateDoc(field.Documentation)}");

        var staticMod = field.IsStatic ? "static " : "";
        var offset = field.Offset.HasValue ? $" // Offset: {field.Offset}" : "";
        sb.AppendLine($"    public {staticMod}{field.Type} {field.Name};{offset}");
    }

    private void RenderProperty(StringBuilder sb, PropertyMetadata prop)
    {
        if (!string.IsNullOrEmpty(prop.Documentation))
            sb.AppendLine($"    /// {TruncateDoc(prop.Documentation)}");

        var staticMod = prop.IsStatic ? "static " : "";
        sb.AppendLine($"    public {staticMod}{prop.Type} {prop.Name} {{ get; set; }}");
    }

    private void RenderMethod(StringBuilder sb, MethodMetadata method)
    {
        if (!string.IsNullOrEmpty(method.Documentation))
        {
            // Split documentation into lines and add as comments
            var docLines = method.Documentation.Split('\n');
            foreach (var line in docLines.Take(3)) // Limit doc lines for conciseness
            {
                sb.AppendLine($"    /// {line.Trim()}");
            }
            if (docLines.Length > 3)
                sb.AppendLine("    /// ...");
        }

        var staticMod = method.IsStatic ? "static " : "";
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        sb.AppendLine($"    public {staticMod}{method.ReturnType} {method.Name}({parameters});");
    }

    private string GetTypeSignature(TypeMetadata type)
    {
        var counts = new List<string>();
        if (type.MethodCount > 0) counts.Add($"{type.MethodCount}m");
        if (type.PropertyCount > 0) counts.Add($"{type.PropertyCount}p");
        if (type.FieldCount > 0) counts.Add($"{type.FieldCount}f");

        var countStr = counts.Any() ? $" ({string.Join(", ", counts)})" : "";
        return $"{type.TypeKind.ToLower()} {type.Name}{countStr}";
    }

    private string TruncateDoc(string documentation, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(documentation))
            return "";

        var firstLine = documentation.Split('\n').First().Trim();
        if (firstLine.Length <= maxLength)
            return firstLine;

        return firstLine.Substring(0, maxLength - 3) + "...";
    }
}