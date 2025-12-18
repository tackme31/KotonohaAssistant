# CLAUDE\.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KotonohaAssistant is a Japanese voice assistant system featuring two VOICEROID characters (琴葉茜/Kotonoha Akane and 琴葉葵/Kotonoha Aoi) powered by OpenAI and A.I. VOICE Editor. The system uses Named Pipes for inter-process communication between multiple applications.

**IMPORTANT**: Do not access or read the `.env` file. Environment variables should be referenced through documentation only.

## Build and Run Commands

### Building the Solution
```bash
# Build entire solution
dotnet build KotonohaAssistant.sln

# Build specific project
dotnet build KotonohaAssistant.Core/KotonohaAssistant.Core.csproj
```

### Building Individual Projects

Projects in this solution have different build requirements based on their target framework:

**Modern .NET Projects** (use `dotnet build`):
```bash
# Core library
dotnet build KotonohaAssistant.Core/KotonohaAssistant.Core.csproj

# AI library
dotnet build KotonohaAssistant.AI/KotonohaAssistant.AI.csproj

# Console interface
dotnet build KotonohaAssistant.Cli/KotonohaAssistant.Cli.csproj

# Alarm app (WPF)
dotnet build KotonohaAssistant.Alarm/KotonohaAssistant.Alarm.csproj
```

**.NET Framework Projects** (require MSBuild):
```bash
# Voice Server (.NET Framework 4.8.1)
# Locate MSBuild first
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
  -latest -requires Microsoft.Component.MSBuild `
  -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

# Restore and build
& $msbuild KotonohaAssistant.VoiceServer/KotonohaAssistant.VoiceServer.csproj /t:Restore /v:minimal
& $msbuild KotonohaAssistant.VoiceServer/KotonohaAssistant.VoiceServer.csproj /p:Configuration=Release /p:Platform=AnyCPU /t:Build /v:minimal
```

**MAUI Projects** (require MSBuild):
```bash
# Voice UI (MAUI + Blazor, .NET 9.0)
# Locate MSBuild (same as above)
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
  -latest -requires Microsoft.Component.MSBuild `
  -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

# Restore and build
& $msbuild KotonohaAssistant.Vui/KotonohaAssistant.Vui.csproj /p:Configuration=Release /p:Platform=AnyCPU /t:Restore,Rebuild /v:minimal
```

**Note for AI Code Verification**:
- For quick verification of Modern .NET projects, use `dotnet build`
- For VoiceServer or Vui projects, either:
  - Build through Visual Studio
  - Use the full MSBuild commands above
  - Run `dotnet build KotonohaAssistant.sln` to build all projects together

### Running Applications

**Multi-Startup Configurations** (via KotonohaAssistant.slnLaunch):
- **CLI App**: Launches VoiceServer + Alarm + Cli console
- **VUI App**: Launches VoiceServer + Alarm + Vui (MAUI app)

**Individual Projects**:
```bash
# Console interface
dotnet run --project KotonohaAssistant.Cli/KotonohaAssistant.Cli.csproj

# Voice UI (MAUI)
dotnet run --project KotonohaAssistant.Vui/KotonohaAssistant.Vui.csproj

# Voice Server (.NET Framework 4.8.1)
# Must run from Visual Studio or built executable

# Alarm App (WPF)
dotnet run --project KotonohaAssistant.Alarm/KotonohaAssistant.Alarm.csproj
```

### Release Build

**Directory Structure**:
```
/publish
    /{version}                    # Auto-detected from project version
        .env                      # Auto-copied from .env.example
        start.bat                 # VUI launcher script
        start-cli.bat             # CLI launcher script
        README.md
        LICENSE
        THIRD-PARTY-NOTICES       # Includes NuGet package licenses
        /assets                   # Resource files
        /prompts                  # AI prompt templates
        /KotonohaAssistant.Alarm
            KotonohaAssistant.Alarm.exe
            (other DLLs and supporting files)
        /KotonohaAssistant.VoiceServer
            KotonohaAssistant.VoiceServer.exe
            (A.I. VOICE API DLLs - excluding Editor-specific DLLs)
        /KotonohaAssistant.Vui
            KotonohaAssistant.Vui.exe
            (other DLLs and supporting files)
        /KotonohaAssistant.Cli
            KotonohaAssistant.Cli.exe
            (other DLLs and supporting files)
```

**Build Command**:
```powershell
.\build.ps1
```

Version is automatically detected from `KotonohaAssistant.AI.csproj` using MSBuild property extraction.

**What the Build Script Does**:
1. **Auto-detects version** from `KotonohaAssistant.AI.csproj` via MSBuild
2. **Locates MSBuild** automatically using vswhere.exe or fallback paths
3. **Builds each project** based on type:
   - **Modern .NET** (Alarm, Cli): Uses `dotnet publish` with `--self-contained false`
   - **.NET Framework** (VoiceServer): Uses MSBuild with NuGet restore
   - **MAUI** (Vui): Uses MSBuild targeting `net9.0-windows10.0.19041.0` framework
4. **Removes debug symbols**: Deletes all `.pdb` files from build outputs
5. **Removes A.I. VOICE Editor DLLs** from VoiceServer output (should not be redistributed):
   - `AI.Framework.dll`
   - `AI.Talk.dll`
   - `AI.Talk.Editor.Api.dll`
   - `System.Text.Json.dll`
6. **Copies additional files** to publish folder:
   - `.env.example` → `.env`
   - `start.bat`, `start-cli.bat`
   - `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES`
   - `assets/`, `prompts/` folders
7. **Appends NuGet license information** to `THIRD-PARTY-NOTICES` using `nuget-license.exe`
8. **Displays build summary** with version and output path

**Customizing Build**:
- Edit project list in `build.ps1` (lines 15-37)
- Add new project types by creating `Build-{Type}Project` function
- Configure output paths via `$publishRoot` variable (line 9)
- Modify DLL removal list in `Remove-AIEditorDLLs` function (lines 178-193)


## Architecture

### Multi-Process Communication Pattern

The system uses a **multi-process architecture** with Named Pipe IPC:

```
┌────────────────────────────────────────────────────┐
│  Client Apps (CLI/VUI)                             │
│  - User interaction                                │
│  - Conversation management via ConversationService │
│  - Speech recognition (VUI only)                   │
└─────────┬───────────────────────┬──────────────────┘
          │ Named Pipes           │ Named Pipes
          ▼                       ▼
┌─────────────────────┐  ┌──────────────────────┐
│ VoiceServer         │  │ Alarm App            │
│ (.NET Fw 4.8.1)     │  │ (WPF + Named Pipes)  │
│ - A.I. VOICE API    │  │ - Timer/Alarm UI     │
│ - TTS synthesis     │  │ - SQLite storage     │
│ - Audio playback    │  │ - Audio playback     │
│ - Speaker switching │  │                      │
└─────────────────────┘  └──────────────────────┘
```

**Pipe Names** (defined in `KotonohaAssistant.Core/Const.cs`):
- `KotonohaAssistant.VoiceServer` - Voice synthesis commands
- `KotonohaAssistant.Alarm` - Alarm/timer management

### Project Structure

| Project | Framework | Purpose |
|---------|-----------|---------|
| **KotonohaAssistant\.Core** | .NET 9.0 | Shared models, IPC clients (VoiceClient/AlarmClient), utilities |
| **KotonohaAssistant\.AI** | .NET 9.0 | OpenAI integration, conversation logic, function calling |
| **KotonohaAssistant\.Cli** | .NET 9.0 | Console REPL interface |
| **KotonohaAssistant\.Vui** | .NET 9.0 MAUI+Blazor | Voice-activated UI with speech recognition |
| **KotonohaAssistant\.VoiceServer** | .NET Framework 4.8.1 | Named Pipe server wrapping A.I. VOICE Editor API |
| **KotonohaAssistant\.Alarm** | .NET 9.0 WPF | Alarm/timer management with UI |
| **KotonohaAssistant\.CharacterView** | .NET Framework 4.8 WPF | Character display window |

### Why .NET Framework for VoiceServer?

The A.I. VOICE Editor API is **only available for .NET Framework**. VoiceServer acts as a bridge, allowing modern .NET 8/9 apps to access voice synthesis via Named Pipes.

## Key Architectural Patterns

### 1. Dual-Personality Conversation System

`ConversationService` (in `KotonohaAssistant.AI/Services/ConversationService.cs`) manages conversations with two distinct AI personalities sharing one conversation context:

- **ConversationState** tracks:
  - `CurrentSister` - Active character (Akane or Aoi)
  - `PatienceCount` - Consecutive requests to same sister (triggers laziness)
  - `ChatMessages` - OpenAI-format conversation history

- **System prompts** are dynamically injected per request based on `CurrentSister`, not stored in history

- **Character switching** happens via:
  - Automatic: Detecting "茜ちゃん" or "葵ちゃん" in user input
  - Laziness: Sister refuses task and delegates to the other

### 2. "Laziness" Feature

Sisters occasionally refuse tasks and pass them to each other (`ConversationService.cs:441-467`):

**Trigger Conditions**:
- 10% random probability on function calls
- After 4+ consecutive function calls to same sister
- Only for functions with `CanBeLazy = true`

**Never lazy for**: StopAlarm, StartTimer, StopTimer, ForgetMemory

**Implementation Flow**:
1. `ShouldBeLazy()` returns true
2. Add instruction to current sister to refuse task
3. Generate refusal response (e.g., "葵、任せたで")
4. Switch to other sister
5. Add instruction to accept task
6. Generate acceptance response and execute function

### 3. Function Calling Architecture

Base class: `ToolFunction` (in `KotonohaAssistant.AI/Functions/ToolFunction.cs`)

**Available Functions**:
- `CallMaster` - Set alarm with custom AI-generated voice message
- `StopAlarm` - Stop playing alarm
- `StartTimer` / `StopTimer` - Timer management
- `ForgetMemory` - Clear conversation history
- `GetCalendarEvent` / `CreateCalendarEvent` - Google Calendar (optional)
- `GetWeather` - OpenWeatherMap weather data (optional)

**Function Execution Loop** (`ConversationService.cs:333-381`):
```
User Input → OpenAI Chat Completion
           → If tool_calls exist:
              ├─ Check ShouldBeLazy()
              │  ├─ Yes: Refuse → Switch Sister → Accept → Execute
              │  └─ No: Execute directly
              └─ Loop until no more tool_calls
```

### 4. Named Pipe Communication

**VoiceClient Commands** (`KotonohaAssistant.Core/Utils/VoiceClient.cs`):
```
SPEAK:<json>    - Synthesize and play voice
                  { Kotonoha: Akane|Aoi,
                    Emotion: Calm|Joy|Anger|Sadness,
                    Message: string }
STOP            - Stop current playback
EXPORT:<json>   - Export voice to WAV file
```

**AlarmClient Commands** (`KotonohaAssistant.Core/Utils/AlarmClient.cs`):
```
ADD_ALARM:<json>       - Create alarm
START_TIMER:<json>     - Start countdown timer
STOP_ALARM / STOP_TIMER - Stop playback
```

### 5. JSON-Based AI Input/Output

The AI uses structured JSON for all inputs/outputs (`SystemMessage.cs`):

**Input Format**:
```json
{
  "InputType": "User|Instruction",
  "Text": "user message or instruction"
}
```

**Output Format**:
```json
{
  "Assistant": "Akane|Aoi",
  "Text": "茜ちゃんの返答やで",
  "Emotion": "Calm|Joy|Anger|Sadness"
}
```

This ensures structured responses that can be parsed for TTS synthesis.

## Character Personalities

Defined in `SystemMessage.cs`:

**琴葉茜 (Akane)** - Elder sister:
- Kansai dialect
- Laid-back personality
- Calls sister "葵"
- Example: "せやなぁ"

**琴葉葵 (Aoi)** - Younger sister:
- Standard Japanese
- Responsible personality
- Calls sister "お姉ちゃん"
- Example: "そうですね"

## Data Persistence

**Conversation History** (`ChatMessageRepository.cs`):
- Stored in SQLite: `%LocalAppData%/Kotonoha Assistant/app.db` (or `app.cli.db`)
- Uses Dapper ORM
- Schema: `ChatMessages(Id, Role, Content, Name, ToolCallId, CreatedAt)`

**Alarm/Timer Settings** (`AlarmRepository.cs`):
- Stored in SQLite: `%LocalAppData%/Kotonoha Assistant/alarm.db`
- Persists scheduled alarms across app restarts

## Important Implementation Notes

### Character Switching Logic

When modifying `ConversationService.cs`:
- Sister changes must add an `Instruction` message to conversation history
- Use `Instruction.SwitchSisterTo()` for explicit switches
- Update `_state.CurrentSister` immediately before adding instruction
- Reset `PatienceCount` only after function execution

### Adding New Functions

To add a new tool function:
1. Inherit from `ToolFunction` in `KotonohaAssistant.AI/Functions/`
2. Override: `Description`, `Parameters`, `CanBeLazy`, `Invoke()`
3. Register in `ConversationService` constructor's function list
4. Consider whether function should be eligible for laziness

### VoiceServer Dependencies

VoiceServer requires these DLLs copied from A.I. VOICE installation:
- `AI.Talk.dll`
- `AI.Talk.Editor.Api.dll`
- `AI.Framework.dll`
- `System.Text.Json.dll`
- `System.ValueTuple.dll`

Located in: `%PROGRAMFILES%/AI/AIVoice/AIVoiceEditor/`

### VoiceServer Speaker Switching

VoiceServer supports switching the default audio output device per character:

**Implementation** (`Program.cs:318-346`):
- Uses `CoreAudio` library (`MMDeviceEnumerator`, `MMDevice`)
- Switches default audio endpoint before TTS playback
- Resets to original default device after playback (in `finally` block)
- Graceful cleanup on console exit (Ctrl+C or window close)

**Lifecycle**:
1. `InitializeSpeakerSwitching()` - Load device references at startup
2. `SwitchSpeakerDeviceTo()` - Set system default before each speech
3. `ResetSpeakerDeviceToDefault()` - Restore original default after speech
4. `OnConsoleExit/OnProcessExit` - Cleanup handlers ensure device reset on termination

**Error Handling**:
- If devices are not configured or not found, speaker switching is disabled
- Device list is displayed to help users configure `.env` correctly
- MMDeviceEnumerator is NOT IDisposable (no need for using statement)

### Environment Variables

Configuration is loaded from `.env` file (not committed to git):
- `OPENAI_API_KEY` - Required for AI features
- `ENABLE_CALENDAR_FUNCTION` - Enable Google Calendar integration
- `ENABLE_WEATHER_FUNCTION` - Enable weather queries
- `GOOGLE_API_KEY` - Path to Google credentials JSON
- `CALENDAR_ID` - Google Calendar ID
- `OWM_API_KEY`, `OWM_LAT`, `OWM_LON` - OpenWeatherMap config
- `ENABLE_SPEAKER_SWITCHING` - Enable switching audio output device per character
- `AKANE_SPEAKER_DEVICE_ID` - Audio device ID for Akane
- `AOI_SPEAKER_DEVICE_ID` - Audio device ID for Aoi

## Testing

Run individual functions by simulating conversations through the CLI app:

```bash
dotnet run --project KotonohaAssistant.Cli/KotonohaAssistant.Cli.csproj
```

Test specific features:
- Alarm: "明日9時に起こして"
- Timer: "3分タイマー"
- Calendar: "今日の予定は？"
- Weather: "明日の天気教えて"
- Memory: "今までの会話忘れて"
- Laziness: Make 4+ consecutive function calls to trigger delegation

## Debugging Multi-Process Setup

When debugging in Visual Studio:
1. Use startup configurations in `KotonohaAssistant.slnLaunch`
2. Set breakpoints in target project
3. For VoiceServer issues, check A.I. VOICE Editor is running
4. For Alarm issues, verify Named Pipe server started successfully
5. Use `Logger.Log()` (in `KotonohaAssistant.Core/Utils/Logger.cs`) for debugging - logs to `%LocalAppData%/Kotonoha Assistant/app.log`

## Common Pitfalls

1. **VoiceServer not responding**: Ensure A.I. VOICE Editor is running and DLLs are copied
2. **Function calls looping**: Check `Invoke()` returns valid JSON and doesn't throw
3. **Sister not switching**: Verify instruction messages are added before generating response
4. **Laziness not working**: Confirm function has `CanBeLazy = true` and patience counter > 3
5. **Database errors**: Ensure `%LocalAppData%/Kotonoha Assistant/` directory exists
