using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        var dllPaths = args.Where(File.Exists).ToArray();
        if (dllPaths.Length == 0)
        {
            Console.Error.WriteLine("Error: None of the provided DLL paths exist.");
            Environment.Exit(1);
        }

        var builder = Host.CreateApplicationBuilder(args);

        // Redirect console logging to stderr
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        var extractor = new Extractor(dllPaths);
        var availableNamespaces = extractor.GetAvailableNamespaces();
        var namespaceSummary = availableNamespaces.Any() 
            ? $"Loaded {availableNamespaces.Count} namespaces: {string.Join(", ", availableNamespaces.Take(5))}{(availableNamespaces.Count > 5 ? "..." : "")}"
            : "No namespaces loaded";

        builder.Services.AddSingleton(extractor);
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