# Social Morpho UI Guide

## Main Window (`/morpho` or `/sm` command)

### Layout Overview
The main window is divided into two sections:

#### 1. Quest List Section (Top ~65% of window)
```
┌─────────────────────────────────────────────────────────────────┐
│ Active Quests    [All] [Active] [Completed]      [Add Quest]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ [Social] Get Dotted Three Times                                 │
│ ████████████░░░░░░░░░░░░░░░░░░░░ 2/3                          │
│ [Details] [Reset] [Complete] [Delete]                          │
│ ───────────────────────────────────────────────────────────────│
│                                                                  │
│ [Emote] Hug Four Players                                        │
│ ████████████████████░░░░░░░░░░░ 3/4                           │
│ [Details] [Reset] [Complete] [Delete]                          │
│ ───────────────────────────────────────────────────────────────│
│                                                                  │
│ [Social] Social Butterfly                              [Weekly] │
│ ███████████████████████████████ 5/5                            │
│ ✓ Completed: 2026-02-12 14:23                                  │
│ [Details] [Reset] [Complete] [Delete]                          │
│ ───────────────────────────────────────────────────────────────│
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Features:**
- **Filter Buttons:** All, Active, Completed
- **Add Quest Button:** Opens dialog to create new quest
- **Quest Type Badge:** Color-coded (Social=Blue, Buff=Green, Emote=Orange, Custom=White)
- **Progress Bar:** Visual representation with percentage completion
- **Reset Schedule Badge:** Shows Daily/Weekly if applicable
- **Completion Status:** Green checkmark with timestamp for completed quests
- **Action Buttons:** Details, Reset, Complete, Delete

#### 2. Settings Section (Bottom ~35% of window)
```
├─────────────────────────────────────────────────────────────────┤
│ Settings                                                        │
│                                                                  │
│ ☑ Sound Enabled                                                │
│ Panel Opacity: ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░ 0.95                     │
│ ☑ Compact Mode                                                 │
│ ☑ Show Quest Tracker Overlay                                   │
│ ☑ Auto-show Tracker on Login                                   │
│ ☑ Show Login Notification                                      │
│                                                                  │
│ [Reload Quests from JSON]  [Reset All Quest Progress]          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Features:**
- All checkboxes save automatically when toggled
- Opacity slider adjusts in real-time
- Reload button refreshes quests from JSON file
- Reset button clears all quest progress

---

## Add Quest Dialog

When clicking "Add Quest" button:

```
┌─────────────────────────────────────────┐
│ Add New Quest                           │
├─────────────────────────────────────────┤
│                                         │
│ Quest ID: [     100     ]               │
│                                         │
│ Title: [                              ] │
│                                         │
│ Description:                            │
│ ┌─────────────────────────────────────┐ │
│ │                                     │ │
│ │                                     │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ Type: [Custom          ▼]               │
│                                         │
│ Goal Count: [    5    ]                 │
│                                         │
│ Reset Schedule: [None          ▼]      │
│                                         │
│ [Create Quest]  [Cancel]                │
│                                         │
└─────────────────────────────────────────┘
```

---

## Quest Details Dialog

When clicking "Details" button:

```
┌─────────────────────────────────────────┐
│ Quest Details: Social Butterfly         │
├─────────────────────────────────────────┤
│                                         │
│ ID: 3                                   │
│ Type: Social                            │
│ Progress: 5/5                           │
│ Status: Completed                       │
│ Reset Schedule: Weekly                  │
│                                         │
│ Description:                            │
│ Use 5 social actions with different    │
│ players                                 │
│                                         │
│ Created: 2026-02-12 10:30              │
│ Completed: 2026-02-12 14:23            │
│ Last Reset: 2026-02-10 00:00           │
│                                         │
├─────────────────────────────────────────┤
│                    [Close]              │
└─────────────────────────────────────────┘
```

---

## Quest Tracker Overlay

**Implementation:** ImGui overlay window (not injected into FFXIV's native quest system)

Positioned in top-right corner of screen with FFXIV-native styling:

```
                                    ┌────────────────────────────────────┐
                                    │ Active Quests                      │
                                    ├────────────────────────────────────┤
                                    │                                    │
                                    │ ● Get Dotted Three Times           │
                                    │    ► Receive /doton from other     │
                                    │      players (2/3)                 │
                                    │    ████████████░░░░░░░░░          │
                                    │                                    │
                                    │ ■ Hug Four Players                 │
                                    │    ► /hug different players        │
                                    │      (3/4)                         │
                                    │    ████████████████░░░░            │
                                    │                                    │
                                    └────────────────────────────────────┘
```

**Features (FFXIV-Style):**
- **Semi-transparent background** (75% opacity with FFXIV tint)
- **Golden quest titles** matching FFXIV's signature color (#D4AF37)
- **Cyan objective text** with arrow symbols (►) like native quests
- **Quest type indicators** with colored symbols:
  - ● Social (Light Blue)
  - ◆ Buff (Light Green)
  - ■ Emote (Light Orange)
  - ★ Custom (Gold)
- **Indented objectives** for better hierarchy
- **FFXIV-style progress bars** in cyan/green
- **Improved spacing** between entries
- No title bar, no resize handles
- Shows only active (incomplete) quests
- Auto-sized to content

---

## Color Scheme

### Quest Tracker Overlay (FFXIV-Style):

#### Quest Type Indicator Symbols:
- **Social (●):** Light Blue (0.4, 0.8, 1.0, 1.0)
- **Buff (◆):** Light Green (0.8, 1.0, 0.4, 1.0)
- **Emote (■):** Light Orange (1.0, 0.8, 0.4, 1.0)
- **Custom (★):** FFXIV Gold (0.83, 0.69, 0.22, 1.0)

#### Text Colors:
- **Quest Titles:** FFXIV Golden (0.83, 0.69, 0.22, 1.0) - #D4AF37
- **Objectives/Descriptions:** FFXIV Cyan (0.0, 0.81, 0.82, 1.0) - #00CED1
- **Progress Counters:** FFXIV Cyan (0.0, 0.81, 0.82, 1.0)
- **"Active Quests" Header:** FFXIV Golden (0.83, 0.69, 0.22, 1.0)

#### Progress Bar Colors:
- **Complete (100%):** Bright Green (0.3, 0.9, 0.3, 1.0)
- **In Progress (1-99%):** FFXIV Cyan (0.0, 0.81, 0.82, 0.8)
- **Not Started (0%):** Dark Gray (0.4, 0.4, 0.4, 0.6)

### Main Window Colors:

#### Quest Type Badge Colors:
- **Social:** Light Blue (0.4, 0.8, 1.0, 1.0)
- **Buff:** Light Green (0.8, 1.0, 0.4, 1.0)
- **Emote:** Light Orange (1.0, 0.8, 0.4, 1.0)
- **Custom:** White (0.9, 0.9, 0.9, 1.0)

#### Other UI Elements:
- **Completed Status:** Green (0.2, 0.8, 0.2, 1.0)
- **Reset Schedule Badge:** Light Purple (0.7, 0.7, 1.0, 1.0)
- **Inactive/Empty:** Gray (0.7, 0.7, 0.7, 1.0)

---

## Login Notification

When logging into FFXIV:

```
Chat Window:
───────────────────────────────────────
[Social Morpho] You have 2 active quests!
───────────────────────────────────────
```

If enabled, the quest tracker overlay will also automatically appear.

---

## Window Sizes

- **Main Window:** 500x700 pixels (default, resizable)
- **Quest Tracker:** Auto-sized, ~280-350 pixels wide
- **Add Quest Dialog:** Shown in main window area
- **Quest Details Dialog:** Shown in main window area

---

## Keyboard Shortcuts

- **`/morpho`** or **`/sm`** - Toggle main window
- **ESC** - Close any dialog or window (standard FFXIV behavior)

---

## Technical Notes

- All windows use ImGui rendering via Dalamud
- Quest tracker has `NoTitleBar | NoResize | AlwaysAutoResize | NoFocusOnAppearing` flags
- Main window uses standard window flags
- **Quest Tracker uses FFXIV-native styling:**
  - Golden quest titles (#D4AF37)
  - Cyan objective text (#00CED1)
  - Quest type indicator symbols (●◆■★)
  - Arrow symbols (►) before objectives
  - Proper indentation and spacing
  - Semi-transparent background (75% opacity)
- Main window follows plugin's custom color scheme
- Font sizes and spacing match native FFXIV UI
