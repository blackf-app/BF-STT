# Technical Specification: Windows Desktop Speech-to-Text App

I want you to build a Windows desktop application in C# (**{.NET 8}**, **{WPF}** preferred) with the following requirements:

## 🎯 Goal
The application records audio from the microphone, sends the recorded audio file to Deepgram's Speech-to-Text REST API, and displays the returned transcript.

**Note:** This is **NOT** real-time streaming.

### The Flow:
1. User presses **"Start Recording"**
2. User speaks
3. User presses **"Stop Recording"**
4. The app sends the recorded audio file to **Deepgram REST API**
5. Receive transcript JSON
6. Extract transcript text
7. Display it in a textbox

## 🔐 Configuration
Use a Deepgram API key stored in:
- `appsettings.json`
- **OR** environment variable `DEEPGRAM_API_KEY`

> [!IMPORTANT]
> Do **NOT** hardcode the API key.

## 🎤 Audio Requirements
- Record microphone input
- Save audio as **WAV** format
- **Format:**
  - 16-bit PCM
  - 16kHz sample rate
  - Mono channel
- Use **NAudio** for recording.

## 🌐 Deepgram API
Use the REST API endpoint:
`POST https://api.deepgram.com/v1/listen`

### Headers:
- `Authorization: Token {API_KEY}`
- `Content-Type: audio/wav`

### Query Parameters:
- `model=nova-3`
- `language=vi`
- `smart_format=true`

## 📦 Response Handling
The JSON response structure:

```json
{
  "results": {
    "channels": [
      {
        "alternatives": [
          {
            "transcript": "recognized text here"
          }
        ]
      }
    ]
  }
}
```

**Extract Path:** `results.channels[0].alternatives[0].transcript`
Display it in a large `TextBox` in the UI.

## 🖥 UI Requirements
**Window Layout:**
- **Button:** `Start Recording`
- **Button:** `Stop Recording`
- **Button:** `Send to Deepgram`
- **Multiline TextBox:** For transcript output
- **Status Label:** (Recording / Sending / Done / Error)

**Interactivity:**
- Disable/Enable buttons appropriately depending on the application state.

## ⚙️ Error Handling
Handle the following scenarios:
- No microphone detected
- Empty audio file
- HTTP errors
- Invalid API key
- Request timeout

Show friendly error messages in the UI.

## 📁 Architecture
- Use **MVVM** pattern.
- **Separate Services:**
  - `AudioRecordingService`
  - `DeepgramService`
  - `MainViewModel`
- Use `async/await` throughout.
- Use `HttpClient` properly (singleton or dependency injected).

## 🧪 Optional Improvements (If easy to implement)
- Automatically send audio immediately after stopping the recording.
- Add a setting to choose the language (e.g., `vi` / `en`).
- Add a loading spinner while sending data.
- Log raw JSON response (debug mode only).

## 🧼 Code Quality
- Clean, readable, and production-ready.
- No hardcoded secrets.
- Comment important parts of the code.
- Provide full solution structure and include `.csproj` dependencies.
- Generate complete, working code.