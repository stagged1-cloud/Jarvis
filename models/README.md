# AI Models Directory

This directory contains the AI models used by Jarvis.

## Required Models

### 1. Hotword Detection (Picovoice Porcupine)
- **File**: `hotword.ppn`
- **Purpose**: Detect "Hey Jarvis" wake word
- **Download**: https://console.picovoice.ai/
- **Size**: ~1-2 MB
- **Setup**:
  1. Sign up for free Picovoice account
  2. Create custom wake word "Hey Jarvis"
  3. Download .ppn file
  4. Place in this directory

### 2. Speech-to-Text (Whisper.net)
- **Directory**: `whisper/`
- **Purpose**: Convert speech to text locally
- **Options**:
  - **ggml-tiny.bin** - Fastest, ~75MB, good for simple commands
  - **ggml-base.bin** - Balanced, ~141MB, better accuracy (recommended)
  - **ggml-small.bin** - Best accuracy, ~466MB, slower
- **Download Script**: 
  ```powershell
  .\scripts\download-whisper-model.ps1 -ModelSize base
  ```
- **Manual**: https://huggingface.co/ggerganov/whisper.cpp/tree/main

### 3. Text-to-Speech (Optional - Piper)
- **Directory**: `tts/`
- **Purpose**: High-quality neural TTS
- **Download**: https://github.com/rhasspy/piper/releases
- **Note**: Windows SAPI is used by default, Piper is optional

### 4. Local LLM (Optional - Ollama)
- **Directory**: `llm/`
- **Purpose**: Local AI reasoning without cloud calls
- **Models**:
  - **llama3.2** - Good general-purpose model
  - **phi-4** - Microsoft's efficient model
  - **qwen2.5** - Good for vision tasks
- **Setup**:
  1. Install Ollama from https://ollama.ai/
  2. Run: `ollama pull llama3.2`
  3. Models stored in Ollama cache, not this directory

## Download Script

Run the included setup script to download all required models:

```powershell
# From project root
.\scripts\download-models.ps1
```

## Current Status

- [x] Hotword model (Hey-Jarvis_en_windows_v3_0_0.ppn) - Included
- [ ] STT model (whisper/ggml-base.bin) - Download required (141MB)
- [ ] TTS model (tts/) - Optional, Windows SAPI used by default
- [ ] LLM setup - Optional, use cloud API or Ollama

## Storage Notes

- **Model files are NOT committed to git** (too large, in .gitignore)
- **Whisper models must be downloaded** using the script above
- Models are large (141MB - 5GB total depending on choices)
- Minimum required: ~141MB (whisper base)
- The Hotword model (Hey-Jarvis_en_windows_v3_0_0.ppn) IS included in the repo
