using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;
using System.Linq;
using System.IO;

namespace SocialMorpho.Windows;


public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;
    private QuestFilter currentFilter = QuestFilter.All;
    private bool showAddQuestDialog = false;
    private ulong newQuestId = 0;
    private string newQuestTitle = string.Empty;
    private string newQuestDescription = string.Empty;
    private QuestType newQuestType = QuestType.Custom;
    private int newQuestGoalCount = 1;
    private ResetSchedule newQuestResetSchedule = ResetSchedule.None;
    private QuestData? selectedQuestForDetails = null;
    private string presetSelection = string.Empty;
    private string packStatusMessage = string.Empty;

    public MainWindow(Plugin plugin, QuestManager questManager) : base("Social Morpho##MainWindow")
    {
        Plugin = plugin;
        QuestManager = questManager;
        presetSelection = string.IsNullOrWhiteSpace(plugin.Configuration.ActiveQuestPreset) ? "Solo" : plugin.Configuration.ActiveQuestPreset;

        Size = new Vector2(500, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (showAddQuestDialog)
        {
            DrawAddQuestDialog();
        }
        else if (selectedQuestForDetails != null)
        {
            DrawQuestDetailsDialog();
        }
        else
        {
            DrawQuestList();
            ImGui.Spacing();
            ImGui.Separator();
            DrawSettings();
        }
    }

    private void DrawQuestList()
    {
        ImGui.Text("Active Quests");
        
        // Filter buttons
        ImGui.SameLine();
        if (ImGui.Button("All"))
            currentFilter = QuestFilter.All;
        ImGui.SameLine();
        if (ImGui.Button("Active"))
            currentFilter = QuestFilter.Active;
        ImGui.SameLine();
        if (ImGui.Button("Completed"))
            currentFilter = QuestFilter.Completed;

        ImGui.SameLine(ImGui.GetWindowWidth() - 110);
        if (ImGui.Button("Add Quest"))
        {
            showAddQuestDialog = true;
            newQuestId = (ulong)(QuestManager.GetAllQuests().Any() ? QuestManager.GetAllQuests().Max(q => q.Id) + 1 : 100);
            newQuestTitle = string.Empty;
            newQuestDescription = string.Empty;
            newQuestType = QuestType.Custom;
            newQuestGoalCount = 1;
            newQuestResetSchedule = ResetSchedule.None;
        }

        ImGui.Separator();

        if (ImGui.BeginChild("##QuestListChild", new Vector2(0, 450)))
        {
            var quests = GetFilteredQuests();
            
            if (quests.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No quests to display");
            }
            else
            {
                foreach (var quest in quests)
                {
                    DrawQuestItem(quest);
                }
            }
            
            ImGui.EndChild();
        }
    }

    private System.Collections.Generic.List<QuestData> GetFilteredQuests()
    {
        var allQuests = QuestManager.GetAllQuests();
        return currentFilter switch
        {
            QuestFilter.Active => allQuests.Where(q => !q.Completed).ToList(),
            QuestFilter.Completed => allQuests.Where(q => q.Completed).ToList(),
            _ => allQuests
        };
    }

    private void DrawQuestItem(QuestData quest)
    {
        // Quest header with type indicator
        var typeColor = GetQuestTypeColor(quest.Type);
        ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
        ImGui.Text($"[{quest.Type}]");
        ImGui.PopStyleColor();
        
        ImGui.SameLine();
        ImGui.TextWrapped($"{quest.Title}");

        // Progress bar
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        var progressColor = GetProgressColor(progress);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(-1, 20), $"{quest.CurrentCount}/{quest.GoalCount}");
        ImGui.PopStyleColor();

        // Completion status
        if (quest.Completed && quest.CompletedAt.HasValue)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), 
                $"✓ Completed: {quest.CompletedAt.Value:yyyy-MM-dd HH:mm}");
        }

        // Reset schedule indicator
        if (quest.ResetSchedule != ResetSchedule.None)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 1.0f, 1.0f), $"[{quest.ResetSchedule}]");
        }

        // Buttons
        if (ImGui.Button($"Details##details{quest.Id}"))
        {
            selectedQuestForDetails = quest;
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset##reset{quest.Id}"))
        {
            QuestManager.ResetQuestProgress(quest.Id);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Complete##complete{quest.Id}"))
        {
            QuestManager.MarkQuestComplete(quest.Id);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Delete##delete{quest.Id}"))
        {
            QuestManager.RemoveQuest(quest.Id);
        }

        ImGui.Spacing();
        ImGui.Separator();
    }

    private void DrawAddQuestDialog()
    {
        ImGui.Text("Add New Quest");
        ImGui.Separator();

        var questIdString = newQuestId.ToString();
if (ImGui.InputText("Quest ID", ref questIdString, 20))
{
    if (ulong.TryParse(questIdString, out var parsed))
        newQuestId = parsed;
}
ImGui.InputText("Title", ref newQuestTitle, 100);
        ImGui.InputTextMultiline("Description", ref newQuestDescription, 500, new Vector2(-1, 60));
        
        if (ImGui.BeginCombo("Type", newQuestType.ToString()))
        {
            foreach (QuestType type in Enum.GetValues(typeof(QuestType)))
            {
                bool isSelected = newQuestType == type;
                if (ImGui.Selectable(type.ToString(), isSelected))
                {
                    newQuestType = type;
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.InputInt("Goal Count", ref newQuestGoalCount);
        if (newQuestGoalCount < 1) newQuestGoalCount = 1;

        if (ImGui.BeginCombo("Reset Schedule", newQuestResetSchedule.ToString()))
        {
            foreach (ResetSchedule schedule in Enum.GetValues(typeof(ResetSchedule)))
            {
                bool isSelected = newQuestResetSchedule == schedule;
                if (ImGui.Selectable(schedule.ToString(), isSelected))
                {
                    newQuestResetSchedule = schedule;
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.Button("Create Quest"))
        {
            var newQuest = new QuestData
            {
                Id = newQuestId,
                Title = newQuestTitle,
                Description = newQuestDescription,
                Type = newQuestType,
                GoalCount = newQuestGoalCount,
                ResetSchedule = newQuestResetSchedule,
                CurrentCount = 0,
                Completed = false
            };
            QuestManager.AddQuest(newQuest);
            showAddQuestDialog = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            showAddQuestDialog = false;
        }
    }

    private void DrawQuestDetailsDialog()
    {
        if (selectedQuestForDetails == null)
            return;

        var quest = selectedQuestForDetails;
        
        ImGui.Text($"Quest Details: {quest.Title}");
        ImGui.Separator();

        ImGui.Text($"ID: {quest.Id}");
        ImGui.Text($"Type: {quest.Type}");
        ImGui.Text($"Progress: {quest.CurrentCount}/{quest.GoalCount}");
        ImGui.Text($"Status: {(quest.Completed ? "Completed" : "In Progress")}");
        ImGui.Text($"Reset Schedule: {quest.ResetSchedule}");
        
        ImGui.Spacing();
        ImGui.TextWrapped("Description:");
        ImGui.TextWrapped(string.IsNullOrEmpty(quest.Description) ? "No description available" : quest.Description);

        ImGui.Spacing();
        ImGui.Text($"Created: {quest.CreatedAt:yyyy-MM-dd HH:mm}");
        if (quest.CompletedAt.HasValue)
        {
            ImGui.Text($"Completed: {quest.CompletedAt.Value:yyyy-MM-dd HH:mm}");
        }
        if (quest.LastResetDate.HasValue)
        {
            ImGui.Text($"Last Reset: {quest.LastResetDate.Value:yyyy-MM-dd HH:mm}");
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Close"))
        {
            selectedQuestForDetails = null;
        }
    }

    private void DrawSettings()
    {
        ImGui.Text("Settings");
        ImGui.Spacing();

        bool soundEnabled = Plugin.Configuration.SoundEnabled;
        bool compactMode = Plugin.Configuration.CompactMode;
        float panelOpacity = Plugin.Configuration.PanelOpacity;
        bool showQuestTracker = Plugin.Configuration.ShowQuestTracker;
        bool showQuestTrackerOnLogin = Plugin.Configuration.ShowQuestTrackerOnLogin;
        bool showLoginNotification = Plugin.Configuration.ShowLoginNotification;
        var presetOptions = new[] { "Solo", "Party", "RP" };

        if (ImGui.Checkbox("Sound Enabled", ref soundEnabled))
        {
            Plugin.Configuration.SoundEnabled = soundEnabled;
            Plugin.Configuration.Save();
        }

        if (ImGui.SliderFloat("Panel Opacity", ref panelOpacity, 0.0f, 1.0f))
        {
            Plugin.Configuration.PanelOpacity = panelOpacity;
            Plugin.Configuration.Save();
        }

        if (ImGui.Checkbox("Compact Mode", ref compactMode))
        {
            Plugin.Configuration.CompactMode = compactMode;
            Plugin.Configuration.Save();
        }

        if (ImGui.Checkbox("Show Quest Tracker Overlay", ref showQuestTracker))
        {
            Plugin.Configuration.ShowQuestTracker = showQuestTracker;
            Plugin.Configuration.Save();
            
            // Update quest tracker window visibility
            if (Plugin.QuestTrackerWindow != null)
            {
                Plugin.QuestTrackerWindow.IsOpen = showQuestTracker;
            }
        }

        if (ImGui.Checkbox("Auto-show Tracker on Login", ref showQuestTrackerOnLogin))
        {
            Plugin.Configuration.ShowQuestTrackerOnLogin = showQuestTrackerOnLogin;
            Plugin.Configuration.Save();
        }

        if (ImGui.Checkbox("Show Login Notification", ref showLoginNotification))
        {
            Plugin.Configuration.ShowLoginNotification = showLoginNotification;
            Plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Text("Daily Quest Preset");
        if (ImGui.BeginCombo("##preset", presetSelection))
        {
            foreach (var option in presetOptions)
            {
                var selected = presetSelection == option;
                if (ImGui.Selectable(option, selected))
                {
                    presetSelection = option;
                    Plugin.Configuration.ActiveQuestPreset = option;
                    Plugin.Configuration.Save();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.Button("Re-roll Daily Quests Now"))
        {
            QuestManager.ForceRerollDailyQuests(DateTime.Now);
            packStatusMessage = "Daily quests re-rolled for current preset.";
        }

        ImGui.Spacing();

        if (ImGui.Button("Reload Quests from JSON"))
        {
            QuestManager.LoadQuestsFromJson(Plugin.PluginInterface.ConfigDirectory.FullName);
            packStatusMessage = "Reloaded quests from Quests.json.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset All Quest Progress"))
        {
            QuestManager.ResetAllQuestProgress();
        }

        if (ImGui.Button("Test Quest Popup"))
        {
            Plugin.TriggerQuestOfferTest();
        }

        ImGui.SameLine();
        if (ImGui.Button("Test Toast Icons"))
        {
            Plugin.TriggerToastIconPreview();
        }

        ImGui.SameLine();
        if (ImGui.Button("Export Quest Pack"))
        {
            var result = QuestManager.ExportQuestPack(Plugin.PluginInterface.ConfigDirectory.FullName);
            packStatusMessage = result.message;
        }

        ImGui.SameLine();
        if (ImGui.Button("Import Quest Pack"))
        {
            var result = QuestManager.ImportQuestPack(Plugin.PluginInterface.ConfigDirectory.FullName);
            packStatusMessage = result.message;
        }

        if (!string.IsNullOrWhiteSpace(packStatusMessage))
        {
            ImGui.TextWrapped(packStatusMessage);
        }

        var importPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "QuestPack.import.json");
        ImGui.TextDisabled($"Import path: {importPath}");

        if (ImGui.CollapsingHeader("Analytics"))
        {
            var stats = QuestManager.GetStats();
            ImGui.Text($"Unlocked Title: {stats.UnlockedTitle}");
            ImGui.Text($"Weekly Rank: {stats.WeeklyRank}");
            ImGui.Text($"Total Progress Ticks: {stats.TotalProgressTicks}");
            ImGui.Text($"Total Completions: {stats.TotalCompletions}");
            ImGui.Text($"Current Streak: {stats.CurrentStreakDays} day(s)");
            ImGui.Text($"Best Streak: {stats.BestStreakDays} day(s)");
            ImGui.Text($"Weekly Completions: {stats.WeeklyCompletions}");

            if (stats.RecentDailyCompletions.Count > 0)
            {
                ImGui.Separator();
                ImGui.Text("Last 14 days:");
                foreach (var day in stats.RecentDailyCompletions)
                {
                    ImGui.Text($"{day.Date}: {day.Count}");
                }
            }
        }
    }

    private Vector4 GetQuestTypeColor(QuestType type)
    {
        return type switch
        {
            QuestType.Social => new Vector4(0.4f, 0.8f, 1.0f, 1.0f),
            QuestType.Buff => new Vector4(0.8f, 1.0f, 0.4f, 1.0f),
            QuestType.Emote => new Vector4(1.0f, 0.8f, 0.4f, 1.0f),
            QuestType.Custom => new Vector4(0.9f, 0.9f, 0.9f, 1.0f),
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }

    private Vector4 GetProgressColor(float progress)
    {
        if (progress >= 1.0f)
            return new Vector4(0.2f, 0.8f, 0.2f, 1.0f);  // Green
        else if (progress > 0f)
            return new Vector4(0.8f, 0.8f, 0.2f, 1.0f);  // Yellow
        else
            return new Vector4(0.5f, 0.5f, 0.5f, 1.0f);  // Gray
    }

    public void Dispose()
    {
    }
}

public enum QuestFilter
{
    All,
    Active,
    Completed
}
