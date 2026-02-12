# Social Morpho - FFXIV Dalamud Plugin



An immersive social quest system for FFXIV that allows you to track social interactions and quests with your fellow adventurers.



## Features



🦋 **Social Quest Tracking** - Create custom quests for social interactions with party members

🎨 **FFXIV UI Theme** - Styled to seamlessly blend with FFXIV's native interface

📊 **Progress Bars** - Visual progress tracking for each quest

⚙️ **Full Management** - Add, edit, delete, and reset quests on the fly

🔊 **Audio Feedback** - Optional sound notifications when quests are completed

💾 **Persistent Storage** - All quests are automatically saved between sessions

🎯 **Quest Types** - Social, Buff, Emote, and Custom quest types

🔄 **Quest Reset Schedules** - Daily and Weekly automatic quest resets

📋 **Quest Tracker Overlay** - FFXIV-style quest tracker that shows active quests on-screen

📦 **JSON Quest Loading** - Load custom quests from external JSON files

🔔 **Login Notifications** - Get notified about active quests when you log in



## Default Quests



The plugin comes with three example quests:

- **Get Dotted Three Times** - Receive DoT effects from 3 different players

- **Hug Four Players** - Use the hug emote on 4 different party members  

- **Social Butterfly** - Use 5 social actions with different players



## Commands



- `/morpho` or `/sm` - Opens the Social Morpho quest menu



## Installation



1. Open your XIVLauncher

2. Go to Settings → Plugins → Plugin Installer

3. Search for "Social Morpho"

4. Click Install



Alternatively, add this repository to your plugin list in XIVLauncher:



## Quest Management



### Creating Quests

Click the "Add Quest" button in the main window to create a new quest with:
- Custom title and description
- Quest type (Social, Buff, Emote, Custom)
- Goal count (number of times to complete)
- Reset schedule (None, Daily, Weekly)

### Viewing Quest Details

Click the "Details" button on any quest to view:
- Full description
- Progress statistics
- Creation and completion dates
- Last reset date (for recurring quests)

### Quest Filtering

Filter quests by status:
- **All** - Shows all quests
- **Active** - Shows incomplete quests only
- **Completed** - Shows completed quests only

### Quest Actions

Each quest has the following actions:
- **Details** - View full quest information
- **Reset** - Reset quest progress to 0
- **Complete** - Mark quest as complete
- **Delete** - Remove quest permanently



## Quest Tracker Overlay



The quest tracker overlay displays active quests on the right side of your screen, similar to FFXIV's native quest tracker. Features include:

- Shows only incomplete quests
- Displays quest title and progress (e.g., "3/5")
- Color-coded progress bars (Green=Complete, Yellow=In Progress, Gray=Not Started)
- Semi-transparent background that blends with the game UI
- Can be toggled on/off in settings

### Configuration

- **Show Quest Tracker Overlay** - Toggle tracker visibility
- **Auto-show Tracker on Login** - Automatically show tracker when logging in
- **Show Login Notification** - Display chat notification about active quests on login



## JSON Quest Loading



You can create custom quests by editing the `Quests.json` file in your plugin configuration directory:

**Location:** `%APPDATA%\XIVLauncher\pluginConfigs\SocialMorpho\Quests.json`

### Example Quest File

```json
{
  "quests": [
    {
      "id": 1001,
      "title": "Weekly Social Gathering",
      "description": "Attend 3 social events with FC members",
      "type": "Social",
      "goalCount": 3,
      "resetSchedule": "Weekly",
      "currentCount": 0,
      "completed": false
    },
    {
      "id": 1002,
      "title": "Daily Buff Share",
      "description": "Share buffs with 5 different players",
      "type": "Buff",
      "goalCount": 5,
      "resetSchedule": "Daily",
      "currentCount": 0,
      "completed": false
    }
  ]
}
```

### Quest Properties

- **id** (number) - Unique quest identifier
- **title** (string) - Quest name
- **description** (string) - Quest description
- **type** (string) - Quest type: "Social", "Buff", "Emote", or "Custom"
- **goalCount** (number) - Number of times quest must be completed
- **resetSchedule** (string) - Reset frequency: "None", "Daily", or "Weekly"
- **currentCount** (number) - Current progress (optional)
- **completed** (boolean) - Completion status (optional)

### Loading Custom Quests

After editing `Quests.json`, use the "Reload Quests from JSON" button in the settings panel to load your changes without restarting the plugin.



## Quest Reset Schedules



Quests can be configured to reset automatically:

- **None** - Quest never resets automatically
- **Daily** - Quest resets at midnight each day
- **Weekly** - Quest resets at the start of each week (Monday)

The plugin checks for quest resets when you log in and automatically resets any quests that are due.



## Settings



### General Settings
- **Sound Enabled** - Enable/disable audio notifications
- **Panel Opacity** - Adjust window transparency (0.0 - 1.0)
- **Compact Mode** - Use compact UI layout

### Quest Tracker Settings
- **Show Quest Tracker Overlay** - Display the quest tracker on-screen
- **Auto-show Tracker on Login** - Automatically show tracker when logging in
- **Show Login Notification** - Show chat message with active quest count on login

### Quest Management
- **Reload Quests from JSON** - Reload quests from the JSON file
- **Reset All Quest Progress** - Reset progress for all quests



## Development



Built with:
- C# / .NET 10.0
- Dalamud Plugin Framework
- ImGui for UI rendering



## License



This plugin is open source. See LICENSE for details.



## Support



For issues, feature requests, or contributions, please visit the GitHub repository.

