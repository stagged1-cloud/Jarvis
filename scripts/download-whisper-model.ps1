# Download Whisper model for speech-to-text
# Uses ggml format models from Hugging Face

param(
    [string]$ModelSize = "base"  # tiny, base, small, medium, large
)

$modelsDir = Join-Path $PSScriptRoot "..\models\whisper"
New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null

Write-Host "Downloading Whisper $ModelSize model..." -ForegroundColor Cyan

$modelFile = "ggml-$ModelSize.bin"
$modelPath = Join-Path $modelsDir $modelFile

if (Test-Path $modelPath) {
    Write-Host "Model already exists at: $modelPath" -ForegroundColor Yellow
    Write-Host "Delete it first if you want to re-download" -ForegroundColor Yellow
    exit 0
}

# Download from Hugging Face
$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-$ModelSize.bin"

Write-Host "Downloading from: $url" -ForegroundColor Green
Write-Host "Saving to: $modelPath" -ForegroundColor Green

try {
    Invoke-WebRequest -Uri $url -OutFile $modelPath -UseBasicParsing
    Write-Host "Model downloaded successfully!" -ForegroundColor Green
    Write-Host "Model size: $([Math]::Round((Get-Item $modelPath).Length / 1MB, 2)) MB" -ForegroundColor Cyan
}
catch {
    Write-Host "Error downloading model: $_" -ForegroundColor Red
    exit 1
}
