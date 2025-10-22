# Jarvis Model Download Script
# This script helps download required AI models for Jarvis

Write-Host "=== Jarvis AI Assistant - Model Setup ===" -ForegroundColor Cyan
Write-Host ""

$modelsDir = "$PSScriptRoot\..\models"

# Create models directory structure
Write-Host "Creating model directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$modelsDir\stt" | Out-Null
New-Item -ItemType Directory -Force -Path "$modelsDir\tts" | Out-Null
New-Item -ItemType Directory -Force -Path "$modelsDir\llm" | Out-Null

Write-Host "Model directories created." -ForegroundColor Green
Write-Host ""

# Hotword Model (Picovoice Porcupine)
Write-Host "=== Hotword Model (Picovoice Porcupine) ===" -ForegroundColor Cyan
Write-Host "To use hotword detection, you need to:"
Write-Host "1. Sign up at https://console.picovoice.ai/" -ForegroundColor White
Write-Host "2. Create a custom wake word for 'Hey Jarvis'" -ForegroundColor White
Write-Host "3. Download the .ppn file" -ForegroundColor White
Write-Host "4. Place it at: $modelsDir\hotword.ppn" -ForegroundColor White
Write-Host ""

# Check if hotword model exists
if (Test-Path "$modelsDir\hotword.ppn") {
    Write-Host "✓ Hotword model found!" -ForegroundColor Green
} else {
    Write-Host "✗ Hotword model not found" -ForegroundColor Red
}
Write-Host ""

# Speech-to-Text Model (Faster-Whisper)
Write-Host "=== Speech-to-Text Model (Faster-Whisper) ===" -ForegroundColor Cyan
Write-Host "Faster-Whisper models are automatically downloaded on first use." -ForegroundColor White
Write-Host "Recommended models:" -ForegroundColor White
Write-Host "  - tiny.en (~75MB) - Fastest, good for simple commands" -ForegroundColor Gray
Write-Host "  - base.en (~145MB) - Balanced speed and accuracy" -ForegroundColor Gray
Write-Host "  - small.en (~466MB) - Best accuracy" -ForegroundColor Gray
Write-Host ""
Write-Host "Models will be downloaded to: %USERPROFILE%\.cache\huggingface\hub" -ForegroundColor White
Write-Host ""

# Text-to-Speech
Write-Host "=== Text-to-Speech ===" -ForegroundColor Cyan
Write-Host "Windows SAPI 5 is used by default (no download required)." -ForegroundColor Green
Write-Host ""
Write-Host "Optional: Piper TTS for better quality" -ForegroundColor White
Write-Host "Download from: https://github.com/rhasspy/piper/releases" -ForegroundColor White
Write-Host "Place voice models in: $modelsDir\tts\" -ForegroundColor White
Write-Host ""

# Local LLM (Ollama)
Write-Host "=== Local LLM (Optional) ===" -ForegroundColor Cyan
Write-Host "For local AI processing without cloud APIs:" -ForegroundColor White
Write-Host "1. Install Ollama from: https://ollama.ai/" -ForegroundColor White
Write-Host "2. Run: ollama pull llama3.2" -ForegroundColor White
Write-Host "   or: ollama pull phi-4" -ForegroundColor White
Write-Host ""

# Check Ollama installation
try {
    $ollamaVersion = & ollama --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Ollama is installed: $ollamaVersion" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ Ollama not installed" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "=== Setup Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Required for basic functionality:" -ForegroundColor Yellow
Write-Host "  [ ] Hotword model (hotword.ppn) - Manual download required" -ForegroundColor White
Write-Host "  [✓] STT model - Auto-downloaded on first use" -ForegroundColor Green
Write-Host "  [✓] TTS - Windows SAPI (built-in)" -ForegroundColor Green
Write-Host ""
Write-Host "Optional enhancements:" -ForegroundColor Yellow
Write-Host "  [ ] Piper TTS - For better voice quality" -ForegroundColor White
Write-Host "  [ ] Ollama - For local LLM processing" -ForegroundColor White
Write-Host ""

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Download hotword.ppn from Picovoice Console" -ForegroundColor White
Write-Host "2. Update config/appsettings.json with your API keys (if using cloud LLM)" -ForegroundColor White
Write-Host "3. Run the test console: dotnet run --project src/Jarvis.TestConsole" -ForegroundColor White
Write-Host ""

Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
