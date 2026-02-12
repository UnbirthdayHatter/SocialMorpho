# Native Quest Injection Implementation Notes

## Overview
The `NativeQuestInjector.cs` has been fully implemented to inject custom quests into FFXIV's native ToDoList UI. This document describes the implementation and important considerations.

## Implementation Details

### Core Approach
1. Access `RaptureAtkModule` instance for UI data manipulation
2. Get `_ToDoList` addon and verify it's visible
3. Access `StringArrayData` (ID 72) and `NumberArrayData` (ID 73) for quest data
4. Calculate available slots after native quests (max 10 total)
5. Inject quest data using managed memory allocation
6. Update quest count to include custom quests

### Key Methods

#### `InjectCustomQuests()`
- Called every frame via `OnUpdate()` when quest tracker is enabled
- Performs null checks at each step to prevent crashes
- Uses early returns for clean error handling
- Injects quests only when ToDoList addon is visible

#### String Injection
```csharp
stringArrayData->SetValue(questIndex * 3, title, false, true, false);
stringArrayData->SetValue(questIndex * 3 + 1, objectiveText, false, true, false);
```
- Uses `managed=true` parameter so game handles memory allocation
- No manual memory tracking needed for strings
- Each quest uses 2 string slots (title and objective)

#### Number Array Updates
```csharp
numberArrayData->IntArray[questIndex * 10 + 1] = iconId;
numberArrayData->IntArray[questIndex * 10 + 2] = 1;
numberArrayData->IntArray[questIndex * 10 + 3] = quest.CurrentCount;
numberArrayData->IntArray[questIndex * 10 + 4] = quest.GoalCount;
```
- Each quest uses multiple integer slots for metadata
- Stores icon ID, objective count, current progress, and goal count

## Important Configuration Values

### Array IDs
- **StringArrayData ID: 72** - May need adjustment based on game version
- **NumberArrayData ID: 73** - May need adjustment based on game version

These IDs are game-version specific and might need to be updated after FFXIV patches.

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
`AllocateString()` and `FreeAllocatedStrings()` methods are retained but not used in the current implementation. They may be useful for future enhancements.

## Error Handling

### Safety Checks
1. RaptureAtkModule instance check
2. Active quests availability check
3. Addon existence and visibility check
4. String/Number array null checks
5. Available slot calculation

### Exception Handling
All injection attempts are wrapped in try-catch in `OnUpdate()`:
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
FFXIV's ToDoList supports a maximum of 10 quests total (native + custom). The implementation respects this limit.

### Visibility Requirement
Quest injection only occurs when the ToDoList addon is visible. If the player hides their quest tracker, custom quests won't be injected.

### Array ID Stability
The array IDs (72, 73) are reverse-engineered values and may change with game updates. Monitor for:
- Quest tracker not updating
- Game crashes related to UI
- Errors in Dalamud logs about array access

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
   - Test hiding/showing ToDoList addon

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
1. **Dynamic Array ID Detection** - Auto-detect correct array IDs
2. **Better Index Calculation** - Analyze ToDoList structure for exact formulas
3. **Custom Quest Ordering** - Allow user-defined quest priority
4. **Quest Categories** - Group quests by type in the tracker
5. **Conditional Display** - Show/hide quests based on player state

### Performance Optimization
- Cache addon pointer between frames if stable
- Only update when quest data changes
- Throttle updates to reduce CPU usage

## Troubleshooting

### Quests Don't Appear
- Check if ShowQuestTracker is enabled in settings
- Verify ToDoList addon is visible in-game
- Check Dalamud logs for errors
- Confirm array IDs are correct for game version

### Game Crashes
- Array IDs may be incorrect for current game version
- Index formulas may be accessing invalid memory
- Check for null pointer dereferences in logs

### Quest Data Incorrect
- Index formulas may need adjustment
- String encoding issues (UTF-8)
- Icon IDs may be wrong for quest type

## References

- [FFXIVClientStructs GitHub](https://github.com/aers/FFXIVClientStructs)
- [Dalamud Plugin Development Docs](https://dalamud.dev/)
- [AddonToDoList Structure](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonToDoList.cs)
- [StringArrayData Documentation](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/StringArrayData.cs)

## Version History

- **Initial Implementation** (2024-02-12): Fully implemented quest injection with managed memory
- Uses RaptureAtkModule for array access
- Respects 10-quest limit
- Handles native quest offset calculation
- Uses managed string allocation for memory safety
