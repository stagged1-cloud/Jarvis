using Jarvis.AI.LLM;
using Microsoft.Extensions.Logging;

namespace Jarvis.TestConsole;

/// <summary>
/// Simple test for Ollama LLM integration
/// </summary>
public class TestLLM
{
    public static async Task Run()
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  🤖 TESTING OLLAMA LLM SERVICE");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<OllamaLLMService>();
        var llm = new OllamaLLMService(logger);

        Console.WriteLine("✓ Ollama LLM Service initialized\n");

        // Test 1: Simple command
        Console.WriteLine("📝 Test 1: Processing command 'open notepad'");
        Console.WriteLine("─────────────────────────────────────────────────────────\n");
        
        var response1 = await llm.ProcessCommand("open notepad");
        
        Console.WriteLine($"✓ LLM Response:");
        Console.WriteLine($"  Action: {response1.Action}");
        Console.WriteLine($"  Target: {response1.Target}");
        Console.WriteLine($"  Confidence: {response1.Confidence:P0}");
        Console.WriteLine($"  Message: {response1.Message}");
        Console.WriteLine($"  Success: {response1.Success}\n");

        // Test 2: More complex command
        Console.WriteLine("📝 Test 2: Processing command 'search for best pizza near me'");
        Console.WriteLine("─────────────────────────────────────────────────────────\n");
        
        var response2 = await llm.ProcessCommand("search for best pizza near me");
        
        Console.WriteLine($"✓ LLM Response:");
        Console.WriteLine($"  Action: {response2.Action}");
        Console.WriteLine($"  Target: {response2.Target}");
        Console.WriteLine($"  Confidence: {response2.Confidence:P0}");
        Console.WriteLine($"  Message: {response2.Message}\n");

        // Test 3: With screen context
        Console.WriteLine("📝 Test 3: With screen context");
        Console.WriteLine("─────────────────────────────────────────────────────────\n");
        
        var screenContext = "Current window: Visual Studio Code - README.md";
        var response3 = await llm.ProcessCommand("close this", screenContext);
        
        Console.WriteLine($"✓ LLM Response:");
        Console.WriteLine($"  Action: {response3.Action}");
        Console.WriteLine($"  Target: {response3.Target}");
        Console.WriteLine($"  Confidence: {response3.Confidence:P0}");
        Console.WriteLine($"  Message: {response3.Message}\n");

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("✓ All LLM tests complete!");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");
    }
}
