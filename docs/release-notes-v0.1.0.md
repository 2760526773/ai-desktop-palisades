# Release Notes v0.1.0

## AI Desktop Palisades v0.1.0

This release turns the original desktop fence project into an AI-assisted desktop organizer for Windows.

### Highlights

- Added AI desktop classification for top-level desktop items
- Added multi-provider AI support
- Added physical desktop archiving instead of mirror-only display
- Added drag in / drag out interactions between desktop and fences
- Added AI settings UI with provider switching and recommended model dropdowns
- Added fence actions such as collapse, lock, restart, and exit
- Added practical item actions: open, open location, move back to desktop, delete, properties

### Included AI Providers

- OpenAI
- Kimi
- Doubao
- DeepSeek
- Gemini
- Qwen
- Groq
- Grok
- OpenRouter
- Mistral
- Custom

### Important Changes

- Desktop items can now be physically moved into categorized fence storage.
- AI classification falls back to local rules when provider/API is unavailable.
- Existing AI settings are automatically upgraded with missing provider presets.

### Notes

- This is a modified fork of `Xstoudi/Palisades`.
- Upstream project: https://github.com/Xstoudi/Palisades
- Inspiration: https://github.com/Twometer/NoFences
- Commercial reference: https://www.stardock.com/products/fences/

### Known Limitations

- Some providers require account-specific model IDs or endpoint IDs.
- Doubao / Ark may require your own endpoint configuration.
- Menu styling has been kept conservative to prioritize stability.
