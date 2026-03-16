using Example;

partial class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length > 0)
        {
            await RunExampleByName(args[0]);
            return;
        }

        Console.WriteLine("=== Weaviate C# Client Examples ===\n");
        Console.WriteLine("Choose an example to run:");
        Console.WriteLine("  1. Traditional Example (cats.json batch insert)");
        Console.WriteLine("  2. Product Catalog Example (filtering and search)");
        Console.WriteLine("  3. Dependency Injection Example");
        Console.WriteLine("  4. Multiple Clients Example");
        Console.WriteLine("  5. Different Configs Example");
        Console.WriteLine("  6. Configuration Example (from appsettings.json)");
        Console.WriteLine("  7. Lazy Initialization Example");
        Console.WriteLine("  8. Connect Helper Example");
        Console.WriteLine();
        Console.Write("Enter your choice (1-8): ");

        var choice = Console.ReadLine();

        await RunExampleByChoice(choice);
    }

    static async Task RunExampleByChoice(string? choice)
    {
        switch (choice)
        {
            case "1":
                await TraditionalExample.Run();
                break;
            case "2":
                await ProductCatalogExample.Run();
                break;
            case "3":
                await DependencyInjectionExample.Run();
                break;
            case "4":
                await MultipleClientsExample.Run();
                break;
            case "5":
                await DifferentConfigsExample.Run();
                break;
            case "6":
                await ConfigurationExample.RunAsync();
                break;
            case "7":
                await LazyInitializationExample.RunAsync();
                break;
            case "8":
                await ConnectHelperExample.RunAsync();
                break;
            default:
                Console.WriteLine("Invalid choice. Running traditional example...");
                await TraditionalExample.Run();
                break;
        }
    }

    static async Task RunExampleByName(string name)
    {
        switch (name.ToLower())
        {
            case "traditional":
                await TraditionalExample.Run();
                break;
            case "products":
            case "catalog":
                await ProductCatalogExample.Run();
                break;
            case "di":
            case "dependency-injection":
                await DependencyInjectionExample.Run();
                break;
            case "multiple":
            case "multiple-clients":
                await MultipleClientsExample.Run();
                break;
            case "configs":
            case "different-configs":
                await DifferentConfigsExample.Run();
                break;
            case "configuration":
                await ConfigurationExample.RunAsync();
                break;
            case "lazy":
            case "lazy-init":
                await LazyInitializationExample.RunAsync();
                break;
            case "connect":
            case "connect-helper":
                await ConnectHelperExample.RunAsync();
                break;
            default:
                Console.WriteLine($"Unknown example: {name}");
                Console.WriteLine(
                    "Available examples: traditional, products, di, multiple, configs, configuration, lazy, connect"
                );
                break;
        }
    }
}
