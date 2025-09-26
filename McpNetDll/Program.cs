using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpNetDll.Registry;
using McpNetDll.Repository;
using McpNetDll.Helpers;

namespace McpNetDll;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Please provide path(s) to DLL file(s) as command line arguments.");
            Console.Error.WriteLine("Usage: McpNetDll.exe <dll-path1> [dll-path2] [...]");
            Environment.Exit(1);
        }

        var dllPaths = args.Where(File.Exists)
            .Select(PathHelper.ConvertWslPath)
            .ToArray();
            
        if (dllPaths.Length == 0)
        {
            Console.Error.WriteLine("Error: None of the provided DLL paths exist.");
            Environment.Exit(1);
        }

        var builder = Host.CreateApplicationBuilder(args);

        // Redirect console logging to stderr
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        // Register the new architecture components
        var typeRegistry = new TypeRegistry();
        typeRegistry.LoadAssemblies(dllPaths);
        
        var availableNamespaces = typeRegistry.GetAllNamespaces();
        var namespaceSummary = availableNamespaces.Any() 
            ? $"Loaded {availableNamespaces.Count} namespaces: {string.Join(", ", availableNamespaces.Take(5))}{(availableNamespaces.Count > 5 ? "..." : "")}"
            : "No namespaces loaded";

        builder.Services.AddSingleton<ITypeRegistry>(typeRegistry);
        builder.Services.AddSingleton<IMetadataRepository, MetadataRepository>();
        builder.Services.AddSingleton<IMcpResponseFormatter, McpResponseFormatter>();
        
        // Backward compatibility extractor removed; logic now lives in TypeRegistry/Repository
        
        builder.Services
            .AddMcpServer(server => { 
                server.ServerInfo = new() { 
                    Name = $"McpNetDll ({namespaceSummary})", 
                    Version = "1.0.0"
                }; 
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(); // Scans the assembly for [McpServerToolType]

        var host = builder.Build();
        await host.RunAsync();
    }
}