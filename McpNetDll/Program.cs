using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpNetDll;

public class DllPathRegistry
{
    public string[] AllowedPaths { get; }

    public DllPathRegistry(string[] allowedPaths)
    {
        AllowedPaths = allowedPaths;
    }

    public bool IsPathAllowed(string path)
    {
        return AllowedPaths.Contains(path, StringComparer.OrdinalIgnoreCase);
    }
}

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

        builder.Services.AddSingleton(new Extractor(dllPaths));
        builder.Services.AddSingleton(new DllPathRegistry(dllPaths));
        builder.Services
            .AddMcpServer(server => { server.ServerInfo = new() { Name = "McpNetDll", Version = "1.0.0" }; })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(); // Scans the assembly for [McpServerToolType]

        var host = builder.Build();
        await host.RunAsync();
    }
}