# PIA MVP Implementation Learnings

## Task 1: AISettings Configuration Model + ISettingsService Integration

### What Was Done
- Created `Pigeon_Uno.Core/Models/AI/AISettings.cs` with nested classes following AppSettings pattern
- Added AISettings property to AppSettings.cs
- Updated JsonSettingsService.cs to serialize AISettings as SINGLE JSON key "AISettings"
- Created TDD tests in `Pigeon_Uno.Tests/Services/AI/AISettingsTests.cs`

### Key Patterns Learned

1. **Nested Class Pattern**: AppSettings uses INotifyPropertyChanged with SetProperty helper. AISettings follows same pattern.

2. **JSON Serialization**: JsonSettingsService stores complex objects as single keys. AISettings is stored via `_settings["AISettings"] = _appSettings.AI` and retrieved via ConvertToAISettings helper.

3. **Default Values from Spec**:
   - OpenRouter base URL: `https://openrouter.ai/api/v1`
   - Gemini Flash-Lite models for most tasks
   - DeepSeek V4 for maintenance/performance scoring
   - 30s analysis interval
   - 5min (300s) cache TTL

### Files Created/Modified
- Created: `Pigeon_Uno.Core/Models/AI/AISettings.cs`
- Created: `Pigeon_Uno.Tests/Services/AI/AISettingsTests.cs`
- Modified: `Pigeon_Uno.Core/Models/AppSettings.cs` (added AI property)
- Modified: `Pigeon_Uno.Core/Services/JsonSettingsService.cs` (added AISettings handling)

### Verification
- Core build: ✅ `dotnet build Pigeon_Uno.Core/Pigeon_Uno.Core.csproj -f net8.0` succeeds
- Tests: ⚠️ Pre-existing error in `PreservationPropertyTests.cs` (unrelated to this task) blocks test run

### Notes
- All docstrings kept as XML comments matching existing codebase style
- INotifyPropertyChanged implemented on all nested config classes
- Uses System.Text.Json (not Newtonsoft.Json) per requirements
