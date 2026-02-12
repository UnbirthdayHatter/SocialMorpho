# Social Morpho Architecture

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         FFXIV + Dalamud                         │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                     Dalamud Services                       │  │
│  │  • IClientState (login events)                             │  │
│  │  • IChatGui (chat messages)                                │  │
│  │  • ICommandManager (slash commands)                        │  │
│  │  • IDalamudPluginInterface (UI rendering)                  │  │
│  │  • IPluginLog (logging)                                    │  │
│  └───────────────────────────────────────────────────────────┘  │
│                             ▲                                    │
│                             │ Injection                          │
└─────────────────────────────┼────────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────────┐
│                      Social Morpho Plugin                        │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                      Plugin.cs (Core)                      │  │
│  │  • Initializes all services                                │  │
│  │  • Manages plugin lifecycle                                │  │
│  │  • Registers commands (/morpho, /sm)                       │  │
│  │  • Subscribes to Draw/Config events                        │  │
│  │  • Loads default quests on first run                       │  │
│  └───────────────────────────────────────────────────────────┘  │
│                             │                                    │
│       ┌─────────────────────┼─────────────────────┐             │
│       ▼                     ▼                     ▼             │
│  ┌─────────┐          ┌─────────┐          ┌──────────┐        │
│  │  Data   │          │ Windows │          │ Services │        │
│  │  Layer  │          │  Layer  │          │  Layer   │        │
│  └─────────┘          └─────────┘          └──────────┘        │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                          Data Layer                              │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ QuestData.cs                                               │  │
│  │  • Quest model (Id, Title, Description, Type, etc.)       │  │
│  │  • QuestType enum (Social, Buff, Emote, Custom)          │  │
│  │  • ResetSchedule enum (None, Daily, Weekly)              │  │
│  │  • Properties: GoalCount, CurrentCount, Completed        │  │
│  │  • Timestamps: CreatedAt, CompletedAt, LastResetDate     │  │
│  └───────────────────────────────────────────────────────────┘  │
│                             │                                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ QuestManager.cs                                            │  │
│  │  • Quest CRUD operations                                   │  │
│  │  • LoadQuestsFromJson() - JSON file loading              │  │
│  │  • CheckAndResetQuests() - Auto reset daily/weekly       │  │
│  │  • GetActiveQuests() - Filter active quests              │  │
│  │  • GetCompletedQuests() - Filter completed quests        │  │
│  │  • ResetAllQuestProgress() - Mass reset                  │  │
│  └───────────────────────────────────────────────────────────┘  │
│                             │                                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ QuestLoader.cs                                             │  │
│  │  • Static utility class                                    │  │
│  │  • LoadFromJson() - Deserialize quest file               │  │
│  │  • CreateExampleQuestFile() - Generate template          │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                         Windows Layer                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ MainWindow.cs                                              │  │
│  │  • Primary UI window                                       │  │
│  │  • Quest list with filtering (All/Active/Completed)       │  │
│  │  • Add Quest dialog (create custom quests)               │  │
│  │  • Quest Details dialog (view full info)                 │  │
│  │  • Quest actions (Details/Reset/Complete/Delete)         │  │
│  │  • Settings panel (all configuration options)            │  │
│  │  • Progress bars with color coding                       │  │
│  │  • Quest type badges                                     │  │
│  └───────────────────────────────────────────────────────────┘  │
│                             │                                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ QuestTrackerWindow.cs                                      │  │
│  │  • Quest tracker overlay (FFXIV-style)                    │  │
│  │  • Shows active quests only                               │  │
│  │  • Top-right positioning                                  │  │
│  │  • Semi-transparent background                            │  │
│  │  • No title bar, no resize                                │  │
│  │  • Real-time quest updates                                │  │
│  │  • Color-coded progress bars                              │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                        Services Layer                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ QuestNotificationService.cs                                │  │
│  │  • Subscribes to IClientState.Login event                 │  │
│  │  • Displays chat notification on login                    │  │
│  │  • Shows active quest count                               │  │
│  │  • Auto-shows quest tracker (if configured)              │  │
│  │  • Proper event unsubscription on disposal               │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                      Configuration Layer                         │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Configuration.cs                                           │  │
│  │  • Persistent settings storage                             │  │
│  │  • SavedQuests list                                        │  │
│  │  • SoundEnabled, PanelOpacity, CompactMode               │  │
│  │  • ShowQuestTracker, ShowQuestTrackerOnLogin             │  │
│  │  • ShowLoginNotification                                  │  │
│  │  • Automatic save on changes                              │  │
│  └───────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      External Data Files                         │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Quests.json (Plugin Config Directory)                     │  │
│  │  • Custom quest definitions                                │  │
│  │  • Can be edited manually                                  │  │
│  │  • Reloaded without plugin restart                         │  │
│  │  • Merged with existing quests (no duplicates)            │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow Diagrams

### Quest Loading Flow (Plugin Initialization)

```
Plugin Constructor
      │
      ▼
Load Configuration
      │
      ▼
Initialize Default Quests? ──Yes──► Create 3 default quests
      │ No                            └─► Save to config
      ▼
Initialize QuestManager
      │
      ▼
Load Quests from JSON ──────────► Read Quests.json
      │                            Parse JSON
      │                            Merge with existing
      ▼
Check and Reset Quests ─────────► Check daily/weekly schedules
      │                            Reset if needed
      ▼
Initialize Windows
      │
      ▼
Initialize Services
      │
      ▼
Register Commands ──────────────► /morpho and /sm
      │
      ▼
Subscribe to Events ─────────────► Draw, OpenConfigUi
      │
      ▼
Plugin Ready
```

### Quest Creation Flow (User Action)

```
User clicks "Add Quest"
      │
      ▼
Show Add Quest Dialog
      │
      ▼
User fills form:
  • Quest ID
  • Title
  • Description
  • Type
  • Goal Count
  • Reset Schedule
      │
      ▼
User clicks "Create Quest"
      │
      ▼
Validate input
      │
      ▼
Create QuestData object
      │
      ▼
QuestManager.AddQuest()
      │
      ▼
Check for duplicates ───No──► Add to SavedQuests
      │ Yes                     │
      │                         ▼
      └──► Ignore           Save Configuration
                                 │
                                 ▼
                            Update UI
                                 │
                                 ▼
                          Quest appears in list
```

### Quest Tracker Update Flow

```
WindowSystem.Draw() called every frame
      │
      ▼
QuestTrackerWindow.PreDraw()
      │
      ▼
Check if ShowQuestTracker is enabled
      │
      ├─No──► Hide window
      │
      ▼ Yes
QuestTrackerWindow.Draw()
      │
      ▼
Get active quests from QuestManager
      │
      ▼
For each active quest:
      │
      ├──► Display quest title
      ├──► Display progress (X/Y)
      └──► Draw progress bar with color coding
      │
      ▼
Render complete
```

### Login Notification Flow

```
Player logs into FFXIV
      │
      ▼
IClientState.Login event fired
      │
      ▼
QuestNotificationService.OnLogin()
      │
      ▼
Check if already shown ──Yes──► Exit
      │ No
      ▼
Get active quests
      │
      ▼
ShowLoginNotification enabled? ──Yes──► Display chat message
      │ No                                "[Social Morpho] You have X active quests!"
      ▼
ShowQuestTrackerOnLogin enabled? ──Yes──► Set ShowQuestTracker = true
      │ No                                  └─► Quest tracker appears
      ▼
Done
```

### Quest Reset Flow (Daily/Weekly)

```
Plugin initialization
      │
      ▼
QuestManager.CheckAndResetQuests()
      │
      ▼
For each quest with reset schedule:
      │
      ├──► ResetSchedule = None? ──Yes──► Skip
      │    │ No
      │    ▼
      │   Check last reset date
      │    │
      │    ├──► Daily: Different day? ──Yes──┐
      │    │                                  │
      │    └──► Weekly: New week? ──Yes──────┤
      │                                       │
      │    ┌──────────────────────────────────┘
      │    ▼
      │   Reset quest:
      │    • Completed = false
      │    • CurrentCount = 0
      │    • CompletedAt = null
      │    • LastResetDate = now
      │    │
      └────┘
      │
      ▼
Save configuration if any resets occurred
      │
      ▼
Done
```

## Command Flow

```
User types "/morpho" or "/sm"
      │
      ▼
Dalamud CommandManager processes command
      │
      ▼
Plugin.OnCommand() called
      │
      ▼
MainWindow.Toggle()
      │
      ├──► If closed: Open window
      └──► If open: Close window
      │
      ▼
WindowSystem.Draw() renders window on next frame
```

## Event Subscriptions

```
Plugin Constructor:
┌─────────────────────────────────────────┐
│ PluginInterface.UiBuilder.Draw          │ ──► Plugin.DrawUI()
│                                         │       └──► WindowSystem.Draw()
├─────────────────────────────────────────┤
│ PluginInterface.UiBuilder.OpenConfigUi │ ──► Plugin.DrawConfigUI()
│                                         │       └──► MainWindow.IsOpen = true
├─────────────────────────────────────────┤
│ ClientState.Login                       │ ──► QuestNotificationService.OnLogin()
│                                         │       ├──► Display chat notification
│                                         │       └──► Auto-show quest tracker
└─────────────────────────────────────────┘

Plugin.Dispose():
┌─────────────────────────────────────────┐
│ Unsubscribe all events                  │
│ Remove command handlers                 │
│ Dispose all services                    │
│ Remove all windows                      │
└─────────────────────────────────────────┘
```

## File System Structure

```
%APPDATA%\XIVLauncher\
├── pluginConfigs\
│   └── SocialMorpho\
│       ├── SocialMorpho.json (Configuration)
│       └── Quests.json (Custom quests - optional)
│
└── devPlugins\
    └── SocialMorpho\
        ├── SocialMorpho.dll
        ├── SocialMorpho.json (manifest)
        └── [Dalamud dependencies]
```

## Threading Model

```
Main Thread (Dalamud UI Thread):
┌────────────────────────────────────┐
│ All UI rendering (ImGui)           │
│ All window updates                 │
│ All event handlers                 │
│ All quest operations               │
│ Configuration save/load            │
│ JSON file operations               │
└────────────────────────────────────┘

Note: Plugin is fully single-threaded
      No async operations
      No background threads
      All operations on Dalamud UI thread
```

## Error Handling Strategy

```
Level 1: Try-Catch at Service Level
┌────────────────────────────────────┐
│ QuestLoader.LoadFromJson()         │ ──► Catch IOException
│ QuestManager.LoadQuestsFromJson()  │ ──► Catch JsonException
│ QuestNotificationService.OnLogin() │ ──► Catch Exception
└────────────────────────────────────┘
         │
         ▼ On error
┌────────────────────────────────────┐
│ Log error with IPluginLog.Error()  │
│ Continue execution gracefully      │
│ Preserve existing data             │
└────────────────────────────────────┘

Level 2: Validation at Input Level
┌────────────────────────────────────┐
│ Input validation in UI forms       │
│ Duplicate checking before add      │
│ Null checking throughout           │
└────────────────────────────────────┘
```

## Performance Considerations

```
Optimization Strategy:
┌────────────────────────────────────┐
│ Quest Tracker                       │
├────────────────────────────────────┤
│ • Only draws when visible          │
│ • Caches active quest list         │
│ • Minimal draw calls               │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Quest Reset Checks                 │
├────────────────────────────────────┤
│ • Only runs at plugin init         │
│ • No continuous polling            │
│ • O(n) complexity                  │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ JSON Loading                        │
├────────────────────────────────────┤
│ • Only on init and manual reload   │
│ • Efficient merge algorithm        │
│ • No duplicate checking overhead   │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Memory Footprint                    │
├────────────────────────────────────┤
│ • In-memory quest list only        │
│ • No caching of unnecessary data   │
│ • Minimal service instances        │
└────────────────────────────────────┘
```
