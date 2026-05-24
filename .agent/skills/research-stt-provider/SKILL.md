---
name: Research STT Provider
description: Research and evaluate a Speech-to-Text provider before integrating it into BF-STT. Use before adding a new provider or replacing an existing provider API.
---

# Research STT Provider

Use this skill before implementation. The output should be an integration decision, not just a list of links.

## Inputs

Collect or infer:
- Provider name.
- Target mode: batch, streaming, or both.
- Required languages, especially Vietnamese and English.
- Expected use case: low-latency chat, long dictation, Test Mode comparison, or fallback provider.
- User constraints: price, latency, privacy, region, account availability.

## Research Checklist

Find authoritative provider documentation for:
- Batch endpoint URL and HTTP method.
- Streaming endpoint URL and protocol.
- Authentication method and header format.
- Audio format requirements: sample rate, encoding, channels, container.
- Model IDs and recommended default model.
- Language handling and auto-detection support.
- Response JSON shape for final text, partial text, confidence, utterance boundaries, and errors.
- Streaming control messages: start, audio frame, keepalive, finalization, close/EOS.
- Rate limits, max audio duration, max request size, and concurrency limits.
- Pricing and free-tier constraints.
- Data retention/privacy policy where relevant.

## Fit Assessment For BF-STT

Evaluate:
- Can it support BF-STT batch mode?
- Can it support BF-STT streaming mode without an SDK?
- Does it accept PCM frames compatible with the current audio pipeline?
- Does it provide interim and final transcripts?
- Does it have utterance/end-of-speech events or will BF-STT need local VAD?
- What settings are required in `AppSettings`?
- What UI fields are needed in `SettingsWindow`?
- What tests can be written without real network calls?
- What security risks exist for API key handling and logs?

## Required Output

Return a short implementation brief:

```md
# Provider Research: {Provider}

## Recommendation
Add / Do not add / Add batch only / Add streaming later.

## BF-STT Fit
| Area | Finding |
|---|---|
| Batch | ... |
| Streaming | ... |
| Audio format | ... |
| Vietnamese/English | ... |
| Auth | ... |
| Risk | ... |

## Required Settings
- ...

## Implementation Notes
- ...

## Tests To Add
- ...

## Open Questions
- ...
```

Do not start implementation until the provider's auth, endpoints, model, response shape, and audio format are clear.
