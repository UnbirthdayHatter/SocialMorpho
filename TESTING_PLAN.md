# Social Morpho Testing Plan

## Pre-Testing Setup

### Environment Requirements
- FFXIV installed with XIVLauncher
- Dalamud framework installed
- Plugin configured in Dalamud dev environment OR installed from plugin repository

### Test Data Preparation
1. Clean installation (no existing configuration)
2. Existing installation with saved quests
3. Quests.json file with custom quests in config directory

---

## Test Suite 1: Critical Bug Fixes

### Test 1.1: Command Registration
**Objective:** Verify `/morpho` and `/sm` commands work

**Steps:**
1. Load plugin in FFXIV
2. Open chat window
3. Type `/morpho` and press Enter
4. Type `/sm` and press Enter

**Expected Results:**
- Both commands open the Social Morpho main window
- Window shows quest list and settings
- No error messages in chat or logs

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 1.2: Window Rendering
**Objective:** Verify windows render correctly

**Steps:**
1. Execute `/morpho` command
2. Observe main window appears
3. Check if quest tracker overlay is visible (if enabled in settings)
4. Move windows around screen
5. Resize main window

**Expected Results:**
- Main window appears and is interactive
- Quest tracker overlay appears in top-right (if enabled)
- Windows render without flickering or artifacts
- Window positions are remembered between opens

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 1.3: Default Quests
**Objective:** Verify default quests are created on first install

**Steps:**
1. Delete existing plugin configuration (if any)
2. Load plugin for first time
3. Open main window with `/morpho`
4. Check quest list

**Expected Results:**
- Three default quests appear:
  - "Get Dotted Three Times" (Goal: 3)
  - "Hug Four Players" (Goal: 4)
  - "Social Butterfly" (Goal: 5)
- All quests show 0/X progress
- All quests are marked as incomplete

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 1.4: Proper Disposal
**Objective:** Verify plugin cleans up correctly

**Steps:**
1. Load plugin
2. Open main window
3. Execute `/xlplugins` to open plugin manager
4. Disable Social Morpho plugin
5. Check Dalamud log for errors

**Expected Results:**
- Plugin unloads without errors
- No memory leaks reported
- Commands are unregistered
- Windows close properly

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 2: Quest Management

### Test 2.1: Add Custom Quest
**Objective:** Verify quest creation dialog works

**Steps:**
1. Open main window
2. Click "Add Quest" button
3. Fill in quest details:
   - ID: 500
   - Title: "Test Quest"
   - Description: "A test quest for validation"
   - Type: Custom
   - Goal Count: 10
   - Reset Schedule: None
4. Click "Create Quest"

**Expected Results:**
- Add quest dialog opens
- All fields are editable
- Quest appears in quest list after creation
- Quest has correct properties

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 2.2: View Quest Details
**Objective:** Verify quest details dialog shows correct information

**Steps:**
1. Open main window
2. Click "Details" button on any quest
3. Review displayed information
4. Click "Close"

**Expected Results:**
- Details dialog opens
- Shows all quest properties (ID, Type, Progress, Status, Description)
- Shows timestamps (Created, Completed, Last Reset if applicable)
- Dialog closes properly

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 2.3: Complete Quest
**Objective:** Verify quest completion works

**Steps:**
1. Open main window
2. Find an incomplete quest
3. Click "Complete" button
4. Observe quest status change

**Expected Results:**
- Quest is marked as completed
- Green checkmark appears
- Completion timestamp is displayed
- Progress bar shows 100% (green)

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 2.4: Reset Quest
**Objective:** Verify quest reset works

**Steps:**
1. Open main window
2. Find a completed or in-progress quest
3. Click "Reset" button
4. Observe quest status change

**Expected Results:**
- Quest progress resets to 0/X
- Completed status is removed
- Progress bar shows 0% (gray)

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 2.5: Delete Quest
**Objective:** Verify quest deletion works

**Steps:**
1. Open main window
2. Note the number of quests
3. Click "Delete" button on a quest
4. Observe quest list

**Expected Results:**
- Quest is immediately removed from list
- Other quests remain intact
- Configuration is saved

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 3: Quest Filtering

### Test 3.1: Show All Quests
**Objective:** Verify "All" filter works

**Steps:**
1. Open main window
2. Ensure you have both active and completed quests
3. Click "All" button
4. Observe quest list

**Expected Results:**
- All quests are shown
- Both completed and incomplete quests visible
- Filter button is highlighted/active

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 3.2: Show Active Quests Only
**Objective:** Verify "Active" filter works

**Steps:**
1. Open main window
2. Click "Active" button
3. Observe quest list

**Expected Results:**
- Only incomplete quests are shown
- Completed quests are hidden
- Filter button is highlighted/active

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 3.3: Show Completed Quests Only
**Objective:** Verify "Completed" filter works

**Steps:**
1. Open main window
2. Click "Completed" button
3. Observe quest list

**Expected Results:**
- Only completed quests are shown
- Incomplete quests are hidden
- Filter button is highlighted/active

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 4: Quest Tracker Overlay

### Test 4.1: Quest Tracker Visibility
**Objective:** Verify quest tracker can be toggled

**Steps:**
1. Open main window
2. In settings, check "Show Quest Tracker Overlay"
3. Close main window
4. Observe right side of screen
5. Uncheck the setting
6. Observe tracker disappears

**Expected Results:**
- Quest tracker appears when enabled
- Tracker disappears when disabled
- Setting persists across plugin reloads

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 4.2: Quest Tracker Content
**Objective:** Verify quest tracker shows correct quests

**Steps:**
1. Enable quest tracker overlay
2. Ensure you have both active and completed quests
3. Observe quest tracker

**Expected Results:**
- Only active (incomplete) quests are shown
- Quest titles are visible
- Progress is shown as "X/Y"
- Progress bars are displayed
- Color coding is correct

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 4.3: Quest Tracker Updates
**Objective:** Verify quest tracker updates in real-time

**Steps:**
1. Enable quest tracker overlay
2. Open main window
3. Complete a quest
4. Observe quest tracker

**Expected Results:**
- Completed quest disappears from tracker
- Other quests remain visible
- Updates happen immediately

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 5: JSON Quest Loading

### Test 5.1: Load Quests from JSON
**Objective:** Verify JSON quest loading works

**Steps:**
1. Navigate to plugin config directory: `%APPDATA%\XIVLauncher\pluginConfigs\SocialMorpho`
2. Create or edit Quests.json with custom quests
3. In plugin settings, click "Reload Quests from JSON"
4. Check quest list

**Expected Results:**
- Quests from JSON file are loaded
- No duplicate quests are created
- Existing quests are preserved
- Success message in logs

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 5.2: Invalid JSON Handling
**Objective:** Verify plugin handles malformed JSON gracefully

**Steps:**
1. Create an invalid Quests.json (malformed JSON syntax)
2. Click "Reload Quests from JSON"
3. Check for errors

**Expected Results:**
- Plugin doesn't crash
- Error is logged
- Existing quests are preserved
- User-friendly error message (optional)

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 5.3: Missing JSON File
**Objective:** Verify plugin handles missing JSON file

**Steps:**
1. Delete Quests.json file
2. Load plugin
3. Click "Reload Quests from JSON"

**Expected Results:**
- Plugin loads without errors
- No crash occurs
- Default quests remain

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 6: Quest Reset Schedules

### Test 6.1: Daily Quest Reset
**Objective:** Verify daily quests reset correctly

**Steps:**
1. Create a quest with Daily reset schedule
2. Complete the quest
3. Change system date to next day
4. Reload plugin

**Expected Results:**
- Quest is automatically reset
- Progress returns to 0
- Completed status is removed
- LastResetDate is updated

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 6.2: Weekly Quest Reset
**Objective:** Verify weekly quests reset correctly

**Steps:**
1. Create a quest with Weekly reset schedule
2. Complete the quest
3. Change system date to next Monday
4. Reload plugin

**Expected Results:**
- Quest is automatically reset
- Progress returns to 0
- Completed status is removed
- LastResetDate is updated

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 6.3: No Reset Schedule
**Objective:** Verify quests without reset schedule don't auto-reset

**Steps:**
1. Create a quest with None reset schedule
2. Complete the quest
3. Change system date to next week
4. Reload plugin

**Expected Results:**
- Quest remains completed
- Progress is not reset
- Completed status is preserved

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 7: Login Notifications

### Test 7.1: Login Notification Display
**Objective:** Verify login notification appears

**Steps:**
1. Enable "Show Login Notification" in settings
2. Ensure you have active quests
3. Log out of FFXIV
4. Log back in
5. Observe chat window

**Expected Results:**
- Chat message appears: "[Social Morpho] You have X active quest(s)!"
- Message shows correct number of active quests
- Message appears only once per login

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 7.2: Auto-show Tracker on Login
**Objective:** Verify quest tracker auto-shows on login

**Steps:**
1. Enable "Auto-show Tracker on Login" in settings
2. Disable quest tracker visibility
3. Log out of FFXIV
4. Log back in
5. Observe screen

**Expected Results:**
- Quest tracker automatically appears
- Tracker shows active quests
- Setting persists

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 7.3: Disable Login Notifications
**Objective:** Verify login notifications can be disabled

**Steps:**
1. Disable "Show Login Notification" in settings
2. Log out of FFXIV
3. Log back in
4. Observe chat window

**Expected Results:**
- No chat message appears
- Plugin still functions normally
- Setting persists

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 8: Settings Persistence

### Test 8.1: Settings Save
**Objective:** Verify settings are saved correctly

**Steps:**
1. Open main window
2. Change all settings:
   - Toggle all checkboxes
   - Adjust opacity slider
3. Close main window
4. Reload plugin
5. Open main window again

**Expected Results:**
- All settings are preserved
- Checkbox states match previous session
- Slider value is correct

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 8.2: Reset All Quest Progress
**Objective:** Verify "Reset All Quest Progress" works

**Steps:**
1. Open main window
2. Complete several quests
3. Set some quests to partial progress
4. Click "Reset All Quest Progress"
5. Observe quest list

**Expected Results:**
- All quests are reset to 0 progress
- All completed statuses are removed
- All quests show as incomplete

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 9: UI/UX Testing

### Test 9.1: Progress Bar Display
**Objective:** Verify progress bars display correctly

**Steps:**
1. View quests with various progress levels:
   - 0% progress
   - 50% progress
   - 100% progress
2. Observe progress bar colors

**Expected Results:**
- 0% shows gray bar
- 1-99% shows yellow bar
- 100% shows green bar
- Progress percentage is accurate

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 9.2: Quest Type Colors
**Objective:** Verify quest types are color-coded

**Steps:**
1. Create quests of each type:
   - Social
   - Buff
   - Emote
   - Custom
2. Observe quest type badges

**Expected Results:**
- Social quests are light blue
- Buff quests are light green
- Emote quests are light orange
- Custom quests are white

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 9.3: Window Responsiveness
**Objective:** Verify UI is responsive and smooth

**Steps:**
1. Open main window
2. Scroll through quest list
3. Click buttons rapidly
4. Open/close dialogs quickly
5. Resize window

**Expected Results:**
- No lag or stuttering
- Buttons respond immediately
- Dialogs open/close smoothly
- Window resizing is smooth

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Suite 10: Edge Cases

### Test 10.1: Empty Quest List
**Objective:** Verify plugin handles empty quest list

**Steps:**
1. Delete all quests
2. Observe main window
3. Open quest tracker

**Expected Results:**
- Main window shows "No quests to display"
- Quest tracker shows "No active quests"
- No crashes or errors

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 10.2: Many Quests
**Objective:** Verify plugin handles large number of quests

**Steps:**
1. Create 50+ quests
2. Open main window
3. Scroll through list
4. Apply filters

**Expected Results:**
- All quests are displayed
- Scrolling works smoothly
- Filters work correctly
- No performance degradation

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

### Test 10.3: Very Long Quest Titles
**Objective:** Verify long text is handled properly

**Steps:**
1. Create quest with very long title (100+ characters)
2. Create quest with very long description (500+ characters)
3. Observe in quest list and details dialog

**Expected Results:**
- Text wraps properly
- No text overflow
- UI remains usable

**Status:** ⬜ Not Tested | ✅ Pass | ❌ Fail

---

## Test Results Summary

### Total Tests: 31
- ⬜ Not Tested: ___
- ✅ Passed: ___
- ❌ Failed: ___
- Pass Rate: ___%

### Critical Issues Found:
1. [List any critical issues here]

### Non-Critical Issues Found:
1. [List any non-critical issues here]

### Recommendations:
1. [List any recommendations here]

---

## Sign-off

**Tester Name:** ___________________
**Date:** ___________________
**Environment:** FFXIV Version ___, Dalamud Version ___
**Plugin Version:** ___________________

**Overall Assessment:**
⬜ Ready for Production
⬜ Needs Minor Fixes
⬜ Needs Major Fixes
⬜ Not Ready

**Additional Notes:**
[Add any additional notes here]
