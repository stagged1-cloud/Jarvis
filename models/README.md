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

### 2. Speech-to-Text (Faster-Whisper)
- **Directory**: `stt/`
- **Purpose**: Convert speech to text locally
- **Options**:
  - **tiny.en** - Fastest, ~75MB, good for simple commands
  - **base.en** - Balanced, ~145MB, better accuracy
  - **small.en** - Best accuracy, ~466MB, slower
- **Download**: Automatic on first run via Hugging Face
- **Manual**: https://huggingface.co/guillaumekln/faster-whisper-base.en

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

- [ ] Hotword model (hotword.ppn)
- [ ] STT model (stt/)
- [ ] TTS model (tts/) - Optional
- [ ] LLM setup - Optional, use cloud API or Ollama

## Storage Notes

- **Do not commit model files to git** (they're in .gitignore)
- Models are large (100MB - 5GB total depending on choices)
- Minimum required: ~200MB (hotword + tiny whisper)
- Recommended: ~500MB (hotword + base whisper)
