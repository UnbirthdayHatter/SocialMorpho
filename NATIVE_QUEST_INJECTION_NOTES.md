# Native Quest Injection Implementation Notes

## Overview
The `NativeQuestInjector.cs` has been fully implemented to inject custom quests into FFXIV's native ToDoList UI. This document describes the implementation and important considerations.

## Implementation Details

### Core Approach
1. Access `AtkStage` instance to get string array data using `StringArrayType.ToDoList`
2. Access number array using `NumberArrayType.ToDoList` and cast to `ToDoListNumberArray*`
3. Read current native quest count from `QuestCount` field
4. Calculate available slots after native quests (max 10 total)
5. Inject quest data using string array SetValue with managed memory
6. Update total quest count to include custom quests

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

// Access number array using NumberArrayType enum and cast to typed pointer
var numberArray = AtkStage.Instance()->GetNumberArrayData(NumberArrayType.ToDoList);
var todoNumberArray = (ToDoListNumberArray*)numberArray->IntArray;
```
This approach uses the FFXIVClientStructs enum values and typed struct access for better stability and type safety.

#### String Injection
```csharp
stringArray->SetValue(questSlot * 3, quest.Title, false, true, false);
stringArray->SetValue(questSlot * 3 + 1, objectiveText, false, true, false);
```
- Uses `managed=true` parameter so game handles memory allocation
- No manual memory tracking needed for strings
- Each quest uses 2 string slots (title and objective)

#### Number Array Updates
```csharp
todoNumberArray->QuestTypeIcon[questSlot] = iconId;
todoNumberArray->ObjectiveCountForQuest[questSlot] = 1;
todoNumberArray->ObjectiveProgress[questSlot] = quest.CurrentCount;
todoNumberArray->ButtonCountForQuest[questSlot] = 0;
```
- Uses typed field accessors for better safety and clarity
- Each quest uses dedicated fields in the ToDoListNumberArray struct
- Field names match the actual struct definition from FFXIVClientStructs

## Important Configuration Values

### Array Access Methods
- **StringArrayData**: Accessed via `AtkStage.Instance()->GetStringArrayData(StringArrayType.ToDoList)`
- **NumberArrayData**: Accessed via `AtkStage.Instance()->GetNumberArrayData(NumberArrayType.ToDoList)` and cast to `ToDoListNumberArray*`

These use FFXIVClientStructs enum types and typed struct pointers for better type safety and version stability.

### Array Indexing
- **String Array**: `questSlot * 3` for title, `questSlot * 3 + 1` for objective
- **Number Array**: Uses typed field accessors like `QuestTypeIcon[questSlot]`, `ObjectiveCountForQuest[questSlot]`, etc.

The typed field access provides compile-time safety and matches the actual struct definition.

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
- No need to track allocated pointers

### Simplified Approach
Previous manual memory allocation methods (`AllocateString()`, `FreeAllocatedStrings()`) have been removed since `SetValue` with managed=true handles all memory management automatically.

## Error Handling

### Safety Checks
1. Active quests availability check
2. AtkStage instance and string array null check
3. Number array and ToDoListNumberArray cast null check
4. Native quest count retrieval from QuestCount field
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

### Typed Field Access
The implementation uses typed field accessors (e.g., `QuestTypeIcon[questSlot]`) which provide:
- Compile-time type safety
- Better IDE support and IntelliSense
- Clearer code that matches the actual struct definition
- Reduced risk of indexing errors

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
1. **Custom Quest Ordering** - Allow user-defined quest priority
2. **Quest Categories** - Group quests by type in the tracker
3. **Conditional Display** - Show/hide quests based on player state

### Performance Optimization
- Only update when quest data changes (currently updates every frame)
- Throttle updates to reduce CPU usage
- Cache array pointers if stable across frames

## Troubleshooting

### Quests Don't Appear
- Check if ShowQuestTracker is enabled in settings
- Verify there are available slots (< 10 total quests)
- Check Dalamud logs for errors
- Confirm StringArrayType.ToDoList and NumberArrayType.ToDoList are correct for game version

### Game Crashes
- Array access may be invalid for current game version
- ToDoListNumberArray struct definition may have changed
- Check for null pointer dereferences in logs

### Quest Data Incorrect
- Verify ToDoListNumberArray field names match current FFXIVClientStructs version
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
- **Fixed Implementation** (2026-02-12): Updated to use NumberArrayType.ToDoList with typed field accessors (QuestCount, QuestTypeIcon, etc.) instead of IntArray indexing
