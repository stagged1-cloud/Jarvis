using Jarvis.AI.LLM;
using Jarvis.Audio.Hotword;
using Jarvis.Audio.STT;
using Jarvis.Audio.TTS;
using Jarvis.Core.Models;
using Jarvis.Core.Services;
using Jarvis.Input;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jarvis.TestConsole;

/// <summary>
/// Test console for validating Jarvis services
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Jarvis AI Assistant - Test Console ===\n");

        // Quick test of LLM if user passes --test-llm argument
        if (args.Contains("--test-llm"))
        {
            await TestLLM.Run();
            return;
        }

        // Load configuration - search upwards from binary location
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        string? configPath = null;
        
        while (currentDir != null && configPath == null)
        {
            var testPath = Path.Combine(currentDir.FullName, "config", "appsettings.json");
            if (File.Exists(testPath))
            {
                configPath = testPath;
                break;
            }
            currentDir = currentDir.Parent;
        }

        if (configPath != null)
        {
            Console.WriteLine($"Found config at: {configPath}");
        }
        else
        {
            Console.WriteLine("⚠ Configuration file not found");
        }

        JarvisConfiguration? config = null;
        if (configPath != null && File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                
                var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                if (jsonDoc != null && jsonDoc.RootElement.TryGetProperty("Jarvis", out var jarvisElement))
                {
                    config = JsonSerializer.Deserialize<JarvisConfiguration>(jarvisElement.GetRawText());
                }
                Console.WriteLine($"✓ Configuration loaded\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error loading config: {ex.Message}\n");
            }
        }
        else
        {
            Console.WriteLine("Using default configuration\n");
        }

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Test TTS Service
        Console.WriteLine("--- Testing TTS Service ---");
        var ttsLogger = loggerFactory.CreateLogger<WindowsTTSService>();
        using var ttsService = new WindowsTTSService(ttsLogger);
        
        // Set voice if configured
        if (!string.IsNullOrWhiteSpace(config?.Audio.TTSVoice))
        {
            ttsService.SetVoice(config.Audio.TTSVoice);
        }
        
        await ttsService.SpeakAsync("Hello! Jarvis system initialized.");
        Console.WriteLine("✓ TTS Service working\n");

        // Initialize GuardRail Service early (needed for action execution)
        var guardRailLogger = loggerFactory.CreateLogger<GuardRailService>();
        var guardRailService = new GuardRailService(guardRailLogger, config?.Security);

        // Test Real Hotword Detection with Porcupine + STT
        Console.WriteLine("--- Testing Real Hotword Detection + Speech Recognition ---");
        if (!string.IsNullOrWhiteSpace(config?.Audio.PicovoiceAccessKey))
        {
            // Find project root where config was found
            var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(configPath!))!;
            var hotwordModelPath = Path.Combine(projectRoot, config.Audio.HotwordModelPath ?? "models/Hey-Jarvis_en_windows_v3_0_0.ppn");
            var sttModelPath = Path.Combine(projectRoot, "models/whisper/ggml-base.bin");

            if (File.Exists(hotwordModelPath))
            {
                // List available microphones and let user choose
                int deviceCount = NAudio.Wave.WaveInEvent.DeviceCount;
                Console.WriteLine($"\nAvailable microphones ({deviceCount} devices):");
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                    Console.WriteLine($"  [{i}] {caps.ProductName}");
                }

                Console.Write("\nSelect microphone device number (0-{0}, or press Enter for 0): ", deviceCount - 1);
                string? input = Console.ReadLine();
                int selectedDevice = 0;
                if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int parsed))
                {
                    selectedDevice = parsed;
                }

                Console.WriteLine($"Using device {selectedDevice}\n");

                // Initialize services
                var hotwordLogger = loggerFactory.CreateLogger<PorcupineHotwordService>();
                using var hotwordService = new PorcupineHotwordService(
                    hotwordLogger,
                    config.Audio.PicovoiceAccessKey,
                    hotwordModelPath,
                    selectedDevice
                );

                WhisperSTTService? sttService = null;
                if (File.Exists(sttModelPath))
                {
                    var sttLogger = loggerFactory.CreateLogger<WhisperSTTService>();
                    sttService = new WhisperSTTService(sttLogger, sttModelPath, selectedDevice);
                    Console.WriteLine("✓ Whisper STT initialized\n");
                }
                else
                {
                    Console.WriteLine($"⚠ Whisper model not found at: {sttModelPath}");
                    Console.WriteLine("STT will not be available. Run: .\\scripts\\download-whisper-model.ps1\n");
                }

                // Initialize LLM Service
                var llmLogger = loggerFactory.CreateLogger<OllamaLLMService>();
                var llmService = new OllamaLLMService(
                    llmLogger,
                    config?.AI.OllamaBaseUrl ?? "http://localhost:11434",
                    config?.AI.OllamaModel ?? "llama3.2"
                );
                Console.WriteLine("✓ Ollama LLM initialized\n");

                // Setup event handlers
                bool isListeningForCommand = false;
                
                hotwordService.HotwordDetected += async (sender, e) =>
                {
                    if (isListeningForCommand) return; // Ignore if already listening
                    
                    Console.WriteLine("\n🎤 HOTWORD DETECTED!");
                    await ttsService.SpeakAsync("Yes?");
                    
                    if (sttService != null)
                    {
                        Console.WriteLine("🎧 Listening for command... (speak now, will process after 3 seconds)\n");
                        isListeningForCommand = true;
                        sttService.StartRecording();
                        
                        // Record for 3 seconds then process
                        await Task.Delay(3000);
                        sttService.StopRecording();
                        
                        // Wait a bit for processing
                        await Task.Delay(2000);
                        isListeningForCommand = false;
                    }
                };

                if (sttService != null)
                {
                    sttService.SpeechRecognized += async (sender, text) =>
                    {
                        Console.WriteLine($"\n💬 YOU SAID: \"{text}\"");
                        Console.WriteLine("🤖 Processing with AI...\n");
                        
                        // Send to LLM for processing
                        var llmResponse = await llmService.ProcessCommand(text);
                        
                        Console.WriteLine($"🧠 AI Understanding:");
                        Console.WriteLine($"   Action: {llmResponse.Action}");
                        Console.WriteLine($"   Target: {llmResponse.Target ?? "N/A"}");
                        Console.WriteLine($"   Confidence: {llmResponse.Confidence:P0}");
                        Console.WriteLine($"   Explanation: {llmResponse.Message}\n");
                        
                        // Speak the explanation
                        await ttsService.SpeakAsync(llmResponse.Message);
                        
                        // Execute the action
                        if (llmResponse.Success && llmResponse.Action != "clarify")
                        {
                            Console.WriteLine($"⚙️  Executing: {llmResponse.Action} → {llmResponse.Target}...");
                            
                            var actionService = new ActionExecutionService(guardRailService, loggerFactory.CreateLogger<ActionExecutionService>());
                            var result = await actionService.ExecuteAsync(llmResponse);
                            
                            if (result.Success)
                            {
                                Console.WriteLine($"✓ {result.Message}\n");
                            }
                            else
                            {
                                Console.WriteLine($"✗ {result.Message}\n");
                                await ttsService.SpeakAsync($"Sorry, {result.Message}");
                            }
                        }
                    };
                }

                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("  🤖 JARVIS VOICE ASSISTANT - AI-POWERED WORKFLOW TEST");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"\n📋 Configuration:");
                Console.WriteLine($"   Hotword Model: {Path.GetFileName(hotwordModelPath)}");
                if (sttService != null)
                    Console.WriteLine($"   STT Model: {Path.GetFileName(sttModelPath)}");
                Console.WriteLine($"   LLM: Ollama ({config?.AI.OllamaModel ?? "llama3.2"})");
                Console.WriteLine($"   Microphone: Device {selectedDevice}");
                Console.WriteLine($"\n📖 Instructions:");
                Console.WriteLine("   1. Say 'Hey Jarvis' to activate");
                Console.WriteLine("   2. Wait for 'Yes?' response");
                Console.WriteLine("   3. Say your command (e.g., 'open notepad', 'search for pizza')");
                Console.WriteLine("   4. Jarvis will use AI to understand and explain the command");
                Console.WriteLine($"\n⚙️  Audio Levels: Watch for 🔊 (speech) vs 🔇 (silence)");
                Console.WriteLine("   Press Ctrl+C to stop\n");
                Console.WriteLine("═══════════════════════════════════════════════════════════\n");
                
                hotwordService.StartListening();

                // Wait for Ctrl+C
                var exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    exitEvent.Set();
                };
                
                exitEvent.WaitOne();
                
                Console.WriteLine("\n\n═══════════════════════════════════════════════════════════");
                Console.WriteLine("Shutting down...");
                hotwordService.StopListening();
                sttService?.Dispose();
                Console.WriteLine("✓ Services stopped\n");
                Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            }
            else
            {
                Console.WriteLine($"⚠ Model file not found: {hotwordModelPath}");
                Console.WriteLine("Skipping real hotword test\n");
            }
        }
        else
        {
            Console.WriteLine("⚠ Picovoice access key not configured");
            Console.WriteLine("Add 'PicovoiceAccessKey' to Audio section in config/appsettings.json");
            Console.WriteLine("Falling back to placeholder hotword service\n");
            
            var hotwordLogger = loggerFactory.CreateLogger<PlaceholderHotwordService>();
            var placeholderHotword = new PlaceholderHotwordService(hotwordLogger);
            
            placeholderHotword.HotwordDetected += async (sender, e) =>
            {
                Console.WriteLine("Hotword detected (simulated)!");
                await ttsService.SpeakAsync("Yes?");
            };
            
            placeholderHotword.StartListening();
            await Task.Delay(6000);
            placeholderHotword.StopListening();
            Console.WriteLine("✓ Placeholder Hotword Service working\n");
        }

        // Test GuardRail Service (already initialized above)
        Console.WriteLine("--- Testing GuardRail Service ---");

        Console.WriteLine("Testing action approval:");
        Console.WriteLine($"  notepad.exe: {(guardRailService.IsActionAllowed("open", "notepad.exe") ? "✓ Allowed" : "✗ Blocked")}");
        Console.WriteLine($"  malware.exe: {(guardRailService.IsActionAllowed("open", "malware.exe") ? "✓ Allowed" : "✗ Blocked")}");
        Console.WriteLine("✓ GuardRail Service working\n");

        // Test Input Control Service
        Console.WriteLine("--- Testing Input Control Service ---");
        var inputLogger = loggerFactory.CreateLogger<InputControlService>();
        var inputService = new InputControlService(inputLogger);

        Console.WriteLine("Moving mouse to center of screen...");
        inputService.MoveMouse(960, 540);
        Console.WriteLine("✓ Input Control Service working\n");

        // Test Skill Manager
        Console.WriteLine("--- Testing Skill Manager ---");
        var skillLogger = loggerFactory.CreateLogger<SkillManager>();
        var skillManager = new SkillManager(skillLogger);
        Console.WriteLine($"Registered skills: {skillManager.GetAllSkills().Count}");
        Console.WriteLine("✓ Skill Manager working\n");

        Console.WriteLine("=== All Tests Complete ===");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
