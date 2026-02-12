# Changelog

All notable changes to the Social Morpho plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added - Quest System Implementation (PR #X)

#### Critical Bug Fixes
- **Command Registration**: Properly registered `/morpho` and `/sm` commands with CommandManager
- **Window Rendering**: Fixed window system by subscribing to `PluginInterface.UiBuilder.Draw` event
- **Default Quests**: Added automatic initialization of 3 default quests on first installation:
  - "Get Dotted Three Times" - Receive DoT effects from 3 different players (Social)
  - "Hug Four Players" - Use the hug emote on 4 different players (Emote)
  - "Social Butterfly" - Use 5 social actions with different players (Social)
- **Proper Disposal**: Implemented complete cleanup in `Dispose()` method with command and event unsubscription

#### New Features

##### Quest Tracker Overlay Window
- FFXIV-style quest tracker overlay positioned in top-right corner
- Shows only active (incomplete) quests in real-time
- Displays quest title and progress (e.g., "3/5")
- Color-coded progress bars:
  - Green: Complete (100%)
  - Yellow: In Progress (1-99%)
  - Gray: Not Started (0%)
- Semi-transparent background (70% opacity) blending with FFXIV UI
- Window flags: NoTitleBar, NoResize, AlwaysAutoResize, NoFocusOnAppearing
- Can be toggled via configuration setting

##### JSON Quest Loading System
- External quest file support via `Quests.json` in plugin config directory
- Hot-reload capability without plugin restart
- Automatic quest merging prevents duplicates
- Quest properties supported:
  - Id (unique identifier)
  - Title and Description
  - Type (Social, Buff, Emote, Custom)
  - GoalCount (target number)
  - ResetSchedule (None, Daily, Weekly)
- Comprehensive error handling for malformed/missing JSON files
- Example quest file included with 2 sample quests

##### Quest Reset Schedules
- Added `ResetSchedule` enum with values: None, Daily, Weekly
- Added `LastResetDate` property to track reset timing
- Automatic quest reset on plugin load based on schedule:
  - **Daily**: Resets at midnight each day
  - **Weekly**: Resets at start of week (Monday)
  - **None**: Never auto-resets
- `CheckAndResetQuests()` method validates and resets eligible quests

##### Login Notification System
- Created `QuestNotificationService` for login event handling
- Hooks into `IClientState.Login` event
- Displays chat message on login: "[Social Morpho] You have X active quest(s)!"
- Configurable options:
  - `ShowLoginNotification`: Enable/disable chat message
  - `ShowQuestTrackerOnLogin`: Auto-show tracker on login

##### Enhanced Quest UI
- **Add Quest Dialog**:
  - Create custom quests via UI
  - Input fields: Quest ID, Title, Description, Type, Goal Count, Reset Schedule
  - Dropdown selections for Type and Reset Schedule
  
- **Quest Details Dialog**:
  - Complete quest information display
  - All properties shown (ID, Type, Progress, Status, Description)
  - Timestamps: Created, Completed, Last Reset
  
- **Quest Filtering**:
  - Three filter modes: All, Active, Completed
  - Filter buttons in quest list header
  - Real-time list updates
  
- **Visual Enhancements**:
  - Progress bars using `ImGui.ProgressBar()` with color coding
  - Quest type badges with color coding:
    - Social: Light Blue
    - Buff: Light Green
    - Emote: Light Orange
    - Custom: White
  - Completion timestamps with green checkmarks
  - Reset schedule badges for recurring quests
  
- **Quest Actions**:
  - **Details**: View full quest information
  - **Reset**: Reset progress to 0
  - **Complete**: Mark quest as complete
  - **Delete**: Permanently remove quest

##### Configuration Enhancements
- Added new settings:
  - `ShowQuestTracker`: Toggle quest tracker overlay visibility
  - `ShowQuestTrackerOnLogin`: Auto-show tracker on login
  - `ShowLoginNotification`: Display login chat notification
- Settings save immediately when changed (auto-save)
- Added utility buttons:
  - "Reload Quests from JSON": Hot-reload quest file
  - "Reset All Quest Progress": Mass reset all quests

#### Technical Improvements
- **Service Injections**: Added IClientState, IChatGui, IPluginLog to Plugin constructor
- **Data Model Updates**: Enhanced QuestData with ResetSchedule and LastResetDate
- **Quest Manager Enhancements**:
  - `LoadQuestsFromJson()`: Load quests from external file
  - `CheckAndResetQuests()`: Handle scheduled resets
  - `GetActiveQuests()`: Filter active quests
  - `GetCompletedQuests()`: Filter completed quests
  - `ResetAllQuestProgress()`: Mass reset functionality
  - `AddQuest(QuestData)`: Overload for adding quest objects
- **Error Handling**: Try-catch blocks at all service boundaries
- **Logging**: Comprehensive logging using IPluginLog throughout
- **Memory Management**: Proper disposal of all services and event subscriptions

#### Documentation
- **README.md**: Complete user documentation with usage examples and JSON format
- **IMPLEMENTATION_SUMMARY.md**: Technical implementation details (282 lines)
- **UI_GUIDE.md**: Visual UI layouts, color schemes, and design specifications (215 lines)
- **TESTING_PLAN.md**: 31 comprehensive test cases across 10 test suites (677 lines)
- **ARCHITECTURE.md**: System architecture with diagrams and data flows (444 lines)
- **CHANGELOG.md**: This file documenting all changes

### Changed
- **Plugin.cs**: Complete rewrite with service integration and bug fixes
- **MainWindow.cs**: Major UI overhaul with filtering, dialogs, and enhanced display
- **Configuration.cs**: Added new quest tracker and notification settings
- **QuestData.cs**: Added ResetSchedule enum and LastResetDate property
- **QuestManager.cs**: Enhanced with JSON loading, filtering, and reset logic

### Files Created
1. `Data/QuestLoader.cs` - JSON quest loading utilities (73 lines)
2. `Services/QuestNotificationService.cs` - Login notification handler (58 lines)
3. `Windows/QuestTrackerWindow.cs` - Quest tracker overlay window (111 lines)
4. `Quests.json` - Example quest data file (24 lines)
5. `IMPLEMENTATION_SUMMARY.md` - Implementation documentation (282 lines)
6. `UI_GUIDE.md` - UI design guide (215 lines)
7. `TESTING_PLAN.md` - Testing scenarios (677 lines)
8. `ARCHITECTURE.md` - Architecture documentation (444 lines)
9. `CHANGELOG.md` - This changelog

### Statistics
- **Total Lines Added**: 1,950+
- **Total Lines Removed**: 32
- **Files Changed**: 13
- **New Features**: 15+
- **Bug Fixes**: 4 critical
- **Documentation Pages**: 5
- **Test Cases**: 31

### Breaking Changes
- None (this is the initial implementation of the quest system)

### Migration Guide
- No migration needed for existing users
- First-time users will see 3 default quests automatically created
- Existing quest data is preserved and enhanced with new properties

### Known Issues
- None

### Security
- JSON deserialization uses System.Text.Json (safe, modern)
- All file operations are scoped to plugin config directory
- No SQL injection risks (no database)
- No XSS risks (no web interface)
- Proper input validation on all user inputs

### Performance
- Quest tracker only updates when visible
- JSON loading only at startup and manual reload
- Quest reset check only at plugin initialization
- No continuous polling or background threads
- Minimal memory footprint (in-memory quest list only)

## [1.0.0] - 2026-02-XX (Initial Release)

### Added
- Initial plugin structure
- Basic quest data models
- Window system foundation
- Configuration system

---

## Links
- [Repository](https://github.com/UnbirthdayHatter/SocialMorpho)
- [Issue Tracker](https://github.com/UnbirthdayHatter/SocialMorpho/issues)
- [Pull Requests](https://github.com/UnbirthdayHatter/SocialMorpho/pulls)
