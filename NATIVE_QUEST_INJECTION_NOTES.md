# Native Quest Injection Implementation Notes

## Current Status
⚠️ **String injection is currently disabled** due to compilation issues with `CStringPointer` requiring `InteropGenerator.Runtime` dependency.

**Working**: Quest metadata (icons, progress, counts) infrastructure is in place.  
**Not Working**: String injection (titles, objectives) to native UI.  
**Alternative**: Quests are displayed via `QuestTrackerWindow.cs` ImGui overlay.

## Overview
The `NativeQuestInjector.cs` infrastructure has been implemented to support custom quest injection into FFXIV's native ToDoList UI. Currently, string injection is disabled (see above), but the framework is ready for future implementation when a proper solution is found.

## Implementation Details

### Core Approach
1. Access `AtkStage` instance to get string array data using `StringArrayType.ToDoList`
2. Access `ToDoListNumberArray` instance for quest metadata
3. Read current native quest count from the number array
4. Calculate available slots after native quests (max 10 total)
5. ~~Inject quest data using managed memory allocation~~ (disabled - see Current Status)
6. Update quest metadata (icons, progress) - strings handled by QuestTrackerWindow.cs

### Key Methods

#### `InjectCustomQuests()`
- Called every frame via `OnUpdate()` when quest tracker is enabled
- Performs null checks at each step to prevent crashes
- Uses early returns for clean error handling
- Respects native FFXIV quests by appending custom quests after them

#### Array Access (Modern Approach)
```csharp
// Access string array using StringArrayType enum
var stringArray = AtkStage.Instance()->GetStringArrayData(StringArrayType.ToDoList);

// Access number array using instance method
var numberArray = ToDoListNumberArray.Instance();
```
This approach is more stable than using hardcoded array IDs (72, 73) as it uses the FFXIVClientStructs enum values.

#### String Injection (Not Yet Implemented)
**Note**: String injection is currently disabled due to compilation issues.

The following approach was intended but requires `InteropGenerator.Runtime` dependency:
```csharp
// TODO: Requires InteropGenerator.Runtime dependency
// stringArray->SetValue(questIndex * 3, quest.Title, false, true, false);
// stringArray->SetValue(questIndex * 3 + 1, objectiveText, false, true, false);
```

**Current Implementation**: Quest display uses `QuestTrackerWindow.cs` ImGui overlay instead.

**Future**: Research alternative methods for StringArrayData manipulation without `SetValue()` or consider adding the dependency if necessary.

#### Number Array Updates
```csharp
numberArray->IntArray[questIndex * 10 + 1] = iconId;
numberArray->IntArray[questIndex * 10 + 2] = 1;
numberArray->IntArray[questIndex * 10 + 3] = quest.CurrentCount;
numberArray->IntArray[questIndex * 10 + 4] = quest.GoalCount;
```
- Each quest uses multiple integer slots for metadata
- Stores icon ID, objective count, current progress, and goal count

## Important Configuration Values

### Array Access Methods
- **StringArrayData**: Accessed via `AtkStage.Instance()->GetStringArrayData(StringArrayType.ToDoList)`
- **NumberArrayData**: Accessed via `ToDoListNumberArray.Instance()`

These use FFXIVClientStructs enum types and are more version-stable than hardcoded IDs.

### Array Indexing
- **String Array**: `questIndex * 3` for title, `questIndex * 3 + 1` for objective
- **Number Array**: `questIndex * 10 + offset` for various metadata

These formulas are based on the ToDoList's internal structure and may require adjustment.

### Quest Icons
- **Social Quest**: 61412
- **Buff Quest**: 61413
- **Emote Quest**: 61414

## Memory Management

### Managed Allocation
The implementation uses `SetValue(managed=true)` which:
- Lets the game allocate memory in UI space
- Automatically handles string copying
- Prevents memory leaks from manual allocation
- Allows immediate pointer disposal after call

### Legacy Methods Kept
`AllocateString()` and `FreeAllocatedStrings()` methods are retained for potential future use or alternative allocation strategies.

## Error Handling

### Safety Checks
1. Active quests availability check
2. AtkStage instance and string array null check
3. ToDoListNumberArray instance null check
4. Native quest count retrieval
5. Available slot calculation

### Exception Handling
All injection attempts are wrapped in try-catch in both `OnUpdate()` and `InjectCustomQuests()`:
```csharp
try
{
    InjectCustomQuests();
}
catch (Exception ex)
{
    Plugin.PluginLog.Error($"Failed to inject quests: {ex}");
}
```

## Limitations and Known Issues

### 10 Quest Maximum
FFXIV's ToDoList supports a maximum of 10 quests total (native + custom). The implementation respects this limit by calculating available slots.

### Native Quest Preservation
Custom quests are always appended after native FFXIV quests. If there are already 10 native quests, no custom quests will be injected.

### Index Formula Accuracy
The indexing formulas (`questIndex * 3`, `questIndex * 10`) are based on observations and may need refinement if:
- Quests appear in wrong positions
- Quest data displays incorrectly
- Native quests are overwritten

## Testing Recommendations

### In-Game Testing Required
Since this code interacts with FFXIV's native UI, it must be tested in-game:

1. **Basic Functionality**
   - Enable ShowQuestTracker in settings
   - Add custom quests via the UI
   - Verify quests appear in native ToDoList
   - Check quest styling matches FFXIV's native style

2. **Edge Cases**
   - Test with 0 custom quests (should do nothing)
   - Test with 10+ custom quests (should limit to available slots)
   - Test with native FFXIV quests present
   - Test with 10 native quests (no room for custom)

3. **Memory Safety**
   - Monitor for memory leaks over extended play sessions
   - Check Dalamud logs for allocation errors
   - Verify proper cleanup on plugin disposal

4. **Visual Verification**
   - Quest titles display correctly
   - Progress counters show (X/Y) format
   - Icons display based on quest type
   - Colors match FFXIV's native quest styling

## Future Enhancements

### Potential Improvements
1. **Better Index Calculation** - Analyze ToDoList structure for exact formulas
2. **Custom Quest Ordering** - Allow user-defined quest priority
3. **Quest Categories** - Group quests by type in the tracker
4. **Conditional Display** - Show/hide quests based on player state

### Performance Optimization
- Only update when quest data changes (currently updates every frame)
- Throttle updates to reduce CPU usage
- Cache array pointers if stable across frames

## Troubleshooting

### Quests Don't Appear
- Check if ShowQuestTracker is enabled in settings
- Verify there are available slots (< 10 total quests)
- Check Dalamud logs for errors
- Confirm StringArrayType.ToDoList is correct for game version

### Game Crashes
- Array access may be invalid for current game version
- Index formulas may be accessing invalid memory
- Check for null pointer dereferences in logs

### Quest Data Incorrect
- Index formulas may need adjustment
- String encoding issues (UTF-8)
- Icon IDs may be wrong for quest type

## References

- [FFXIVClientStructs GitHub](https://github.com/aers/FFXIVClientStructs)
- [Dalamud Plugin Development Docs](https://dalamud.dev/)
- [StringArrayType Enum](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/StringArrayType.cs)
- [ToDoListNumberArray](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Misc/ToDoListNumberArray.cs)
- [NoTypeSay Reference Implementation](https://github.com/aethergel/NoTypeSay/blob/9697ad5fb912b0b11738138350792e8aa5998b62/NoTypeSay/Plugin.cs#L5-L95)

## Version History

- **Initial Implementation** (2024-02-12): Implemented using RaptureAtkModule with hardcoded array IDs
- **Modernized Implementation** (2026-02-12): Updated to use StringArrayType.ToDoList and ToDoListNumberArray.Instance() for better version stability
