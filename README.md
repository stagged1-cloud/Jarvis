# Jarvis AI Assistant

A Windows desktop application that provides intelligent AI assistance through voice commands and screen monitoring.

## Features

- **Voice Activation**: Responds to "Hey Jarvis" wake word
- **Real-time Screen Monitoring**: Captures and analyzes screen content
- **Voice Commands**: Execute actions through natural language
- **Safe Operation**: Permission-based action approval system
- **Low Resource Usage**: Optimized for minimal CPU and memory usage
- **Offline-First**: Local processing with optional cloud escalation

## Architecture

### Core Components

- **Jarvis.Core**: Core services and interfaces
- **Jarvis.UI**: WinUI 3 user interface
- **Jarvis.AI**: AI/LLM integration and vision processing
- **Jarvis.Capture**: Screen capture using Windows.Graphics.Capture
- **Jarvis.Input**: Input control via SendInput and UI Automation
- **Jarvis.Audio**: Speech-to-text and text-to-speech services

### Technology Stack

- **Framework**: .NET 9
- **UI**: WinUI 3
- **Screen Capture**: Windows.Graphics.Capture API
- **Input Control**: SendInput + UI Automation
- **Hotword Detection**: Picovoice Porcupine
- **STT**: Faster-Whisper or Vosk
- **TTS**: Windows SAPI 5 or Piper
- **OCR**: Windows.Media.Ocr or Tesseract
- **AI**: Local models via Ollama + optional cloud APIs

## Getting Started

### Prerequisites

- Windows 10 version 1903 or later
- .NET 9 SDK
- Visual Studio 2022 with WinUI 3 workload

### Building

1. Clone the repository
2. Open `Jarvis.sln` in Visual Studio
3. Restore NuGet packages
4. Build the solution

### Configuration

Edit `config/appsettings.json` to configure:
- AI model paths and API keys
- Audio settings
- Security permissions
- Capture settings

## Usage

1. Launch the application
2. Say "Hey Jarvis" to activate
3. Give voice commands or ask questions
4. Approve actions when prompted

## Safety Features

- **Permission Banners**: All actions require explicit approval
- **Kill Switch**: Global hotkey to stop all operations
- **App Whitelist**: Restrict actions to approved applications
- **Session Logging**: Track all AI actions and approvals

## Contributing

Please read CONTRIBUTING.md for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the MIT License - see the LICENSE file for details.