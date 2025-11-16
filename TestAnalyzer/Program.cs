using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestAnalyzer
{
    class TestWebApi
    {
        static async Task Main(string[] args)
        {
            var baseUrl = "http://localhost:5000";
            var dllPath = @"S:\DPB2\DreamPoeBot.dll";

            using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

            try
            {
                // First, load the DLL
                Console.WriteLine($"Loading DLL: {dllPath}");
                var loadResponse = await client.PostAsync($"/api/load?path={Uri.EscapeDataString(dllPath)}", null);
                var loadContent = await loadResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Load response: {loadContent}");

                // Test 1: Search for ClientFunctions
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("Test 1: Searching for 'ClientFunctions'");
                var searchResponse = await client.GetAsync("/api/search?pattern=ClientFunctions");
                var searchJson = await searchResponse.Content.ReadAsStringAsync();
                var searchDoc = JsonDocument.Parse(searchJson);

                if (searchDoc.RootElement.TryGetProperty("Results", out var results))
                {
                    Console.WriteLine($"Found {results.GetArrayLength()} results:");
                    foreach (var result in results.EnumerateArray())
                    {
                        var name = result.GetProperty("Name").GetString();
                        var elementType = result.GetProperty("ElementType").GetString();
                        var fullName = result.TryGetProperty("FullName", out var fn) ? fn.GetString() : "";
                        Console.WriteLine($"  - {elementType}: {fullName ?? name}");
                    }
                }

                // Test 2: Get type details for nested type
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("Test 2: Getting type details for LokiPoe+ClientFunctions");
                var typeResponse = await client.GetAsync($"/api/types?typeNames={Uri.EscapeDataString("DreamPoeBot.Loki.Game.LokiPoe+ClientFunctions")}");
                var typeJson = await typeResponse.Content.ReadAsStringAsync();
                var typeDoc = JsonDocument.Parse(typeJson);

                if (typeDoc.RootElement.TryGetProperty("Types", out var types) && types.GetArrayLength() > 0)
                {
                    var type = types[0];
                    var typeName = type.GetProperty("Name").GetString();
                    var typeKind = type.GetProperty("TypeKind").GetString();
                    var methodCount = type.TryGetProperty("MethodCount", out var mc) ? mc.GetInt32() : 0;

                    Console.WriteLine($"Found type: {typeName}");
                    Console.WriteLine($"  TypeKind: {typeKind}");
                    Console.WriteLine($"  Methods: {methodCount}");

                    if (type.TryGetProperty("Methods", out var methods))
                    {
                        Console.WriteLine("  Sample methods:");
                        foreach (var method in methods.EnumerateArray().Take(5))
                        {
                            var methodName = method.GetProperty("Name").GetString();
                            var isStatic = method.TryGetProperty("IsStatic", out var st) && st.GetBoolean();
                            Console.WriteLine($"    - {methodName} (Static: {isStatic})");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Type not found!");
                }

                // Test 3: Get namespace with nested types
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("Test 3: Getting namespace DreamPoeBot.Loki.Game");
                var nsResponse = await client.GetAsync($"/api/namespaces?namespaces={Uri.EscapeDataString("DreamPoeBot.Loki.Game")}");
                var nsJson = await nsResponse.Content.ReadAsStringAsync();
                var nsDoc = JsonDocument.Parse(nsJson);

                if (nsDoc.RootElement.TryGetProperty("Namespaces", out var namespaces) && namespaces.GetArrayLength() > 0)
                {
                    var ns = namespaces[0];
                    var nsName = ns.GetProperty("Name").GetString();
                    var typeCount = ns.GetProperty("TypeCount").GetInt32();

                    Console.WriteLine($"Namespace: {nsName}");
                    Console.WriteLine($"  Total types: {typeCount}");

                    if (ns.TryGetProperty("Types", out var nsTypes))
                    {
                        var nestedTypes = nsTypes.EnumerateArray()
                            .Where(t => t.GetProperty("Name").GetString().Contains("+"))
                            .ToList();

                        Console.WriteLine($"  Nested types found: {nestedTypes.Count}");
                        Console.WriteLine("  Sample nested types:");

                        foreach (var nested in nestedTypes.Take(10))
                        {
                            var name = nested.GetProperty("Name").GetString();
                            var kind = nested.GetProperty("TypeKind").GetString();
                            Console.WriteLine($"    - {name} ({kind})");
                        }
                    }
                }

                // Test 4: List all types to verify nested types are included
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("Test 4: Verifying nested types in type list");
                var listResponse = await client.GetAsync("/api/types/list");
                var listJson = await listResponse.Content.ReadAsStringAsync();
                var typeList = JsonSerializer.Deserialize<string[]>(listJson);

                var nestedTypeNames = typeList.Where(t => t.Contains("+")).ToArray();
                Console.WriteLine($"Total types: {typeList.Length}");
                Console.WriteLine($"Nested types: {nestedTypeNames.Length}");

                // Check for specific nested types
                var clientFunctions = typeList.FirstOrDefault(t => t.EndsWith("LokiPoe+ClientFunctions"));
                Console.WriteLine($"\nLokiPoe+ClientFunctions found: {clientFunctions != null}");
                if (clientFunctions != null)
                {
                    Console.WriteLine($"  Full name: {clientFunctions}");
                }

                // Show some examples of deeply nested types
                var deeplyNested = nestedTypeNames
                    .Where(t => t.Count(c => c == '+') >= 2)
                    .Take(5)
                    .ToArray();

                if (deeplyNested.Any())
                {
                    Console.WriteLine($"\nDeeply nested types (2+ levels):");
                    foreach (var dn in deeplyNested)
                    {
                        Console.WriteLine($"  - {dn}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Error: {ex.Message}");
                Console.WriteLine("Make sure the web server is running on http://localhost:5000");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}