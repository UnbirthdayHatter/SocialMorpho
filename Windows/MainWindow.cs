using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace SocialMorpho.Windows;


public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;
    private QuestFilter currentFilter = QuestFilter.All;
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
        if (selectedQuestForDetails != null)
        {
            DrawQuestDetailsDialog();
        }
        else
        {
            DrawTopTitleProgress();
            ImGui.Spacing();
            ImGui.Separator();
            DrawQuestList();
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Settings & Analytics"))
            {
                DrawSettings();
            }
        }
    }

    private void DrawTopTitleProgress()
    {
        var progress = QuestManager.GetTitleProgress();

        ImGui.TextUnformatted($"Title: {progress.CurrentTitle}");
        ImGui.SameLine();
        ImGui.TextDisabled($"Next: {progress.NextTitle}");

        var needed = progress.NextRequirement;
        var pct = needed <= 0 ? 1f : Math.Clamp((float)progress.CurrentCompletions / needed, 0f, 1f);
        ImGui.ProgressBar(pct, new Vector2(-1f, 18f), $"{progress.CurrentCompletions}/{needed}");
        ImGui.TextDisabled($"{progress.RemainingToNext} completion(s) to next title");
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

        ImGui.Separator();

        if (ImGui.BeginChild("##QuestListChild", new Vector2(0, 250)))
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
            QuestFilter.Active => QuestManager.GetActiveQuests(),
            QuestFilter.Completed => allQuests.Where(q => q.Completed).ToList(),
            _ => allQuests
        };
    }

    private void DrawQuestItem(QuestData quest)
    {
        var typeColor = GetQuestTypeColor(quest.Type);
        ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
        ImGui.Text($"[{quest.Type}]");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.TextWrapped(quest.Title);

        var progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        var progressColor = GetProgressColor(progress);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(-1, 16), $"{quest.CurrentCount}/{quest.GoalCount}");
        ImGui.PopStyleColor();

        if (quest.ResetSchedule != ResetSchedule.None)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 1.0f, 1.0f), $"[{quest.ResetSchedule}]");
        }

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
        if (ImGui.Button($"Delete##delete{quest.Id}"))
        {
            QuestManager.RemoveQuest(quest.Id);
        }

        ImGui.Separator();
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
        bool soundEnabled = Plugin.Configuration.SoundEnabled;
        float panelOpacity = Plugin.Configuration.PanelOpacity;
        bool showQuestTracker = Plugin.Configuration.ShowQuestTracker;
        bool showQuestTrackerOnLogin = Plugin.Configuration.ShowQuestTrackerOnLogin;
        bool showLoginNotification = Plugin.Configuration.ShowLoginNotification;
        bool showRewardTitleOnNameplate = Plugin.Configuration.ShowRewardTitleOnNameplate;
        bool enableTitleSync = Plugin.Configuration.EnableTitleSync;
        bool shareTitleSync = Plugin.Configuration.ShareTitleSync;
        bool showSyncedTitles = Plugin.Configuration.ShowSyncedTitles;
        var rewardTitleColorPreset = Plugin.Configuration.RewardTitleColorPreset;
        var presetOptions = new[] { "Solo", "Party", "RP" };
        var titleProgress = QuestManager.GetTitleProgress();
        var secretProgress = QuestManager.GetSecretTitleProgress();
        var unlockedSecrets = new HashSet<string>(secretProgress.Where(s => s.Unlocked).Select(s => s.Title), StringComparer.Ordinal);

        var titleColorOptions = new[]
        {
            new RewardColorOption("Gold", null),
            new RewardColorOption("Pink", null),
            new RewardColorOption("Cyan", null),
            new RewardColorOption("Rose", null),
            new RewardColorOption("Mint", null),
            new RewardColorOption("Violet", null),
            new RewardColorOption("Gold Glow", "Butterfly Kisses"),
            new RewardColorOption("Pink Glow", "Boogie Master"),
            new RewardColorOption("Cyan Glow", "Four Eyes"),
            new RewardColorOption("Rose Glow", "Crowd Favorite"),
            new RewardColorOption("Mint Glow", "Thumbs of Approval"),
            new RewardColorOption("Violet Glow", "Victory Lap"),
            new RewardColorOption("White Glow", "Peer Reviewed"),
        };

        var settingsHeight = MathF.Max(220f, ImGui.GetContentRegionAvail().Y);
        if (ImGui.BeginChild("##SettingsScroll", new Vector2(0f, settingsHeight), true))
        {
            if (ImGui.CollapsingHeader("Title Progress"))
            {
                ImGui.Text($"Current Title: {titleProgress.CurrentTitle}");
                ImGui.Text($"Next Title: {titleProgress.NextTitle}");

                var earned = titleProgress.CurrentCompletions;
                var needed = titleProgress.NextRequirement;
                var pct = needed <= 0 ? 1f : Math.Clamp((float)earned / needed, 0f, 1f);
                ImGui.ProgressBar(pct, new Vector2(-1f, 22f), $"{earned}/{needed} completions");
                ImGui.TextDisabled($"{titleProgress.RemainingToNext} completion(s) remaining");
            }

            if (ImGui.CollapsingHeader("Tracker"))
            {
                if (ImGui.Checkbox("Show Quest Tracker Overlay", ref showQuestTracker))
                {
                    Plugin.Configuration.ShowQuestTracker = showQuestTracker;
                    Plugin.Configuration.Save();
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

                if (ImGui.SliderFloat("Panel Opacity", ref panelOpacity, 0.0f, 1.0f))
                {
                    Plugin.Configuration.PanelOpacity = panelOpacity;
                    Plugin.Configuration.Save();
                }
            }

            if (ImGui.CollapsingHeader("Audio"))
            {
                if (ImGui.Checkbox("Sound Enabled", ref soundEnabled))
                {
                    Plugin.Configuration.SoundEnabled = soundEnabled;
                    Plugin.Configuration.Save();
                }
            }

            if (ImGui.CollapsingHeader("Reward Title"))
            {
                if (ImGui.Checkbox("Show Reward Title Above Name", ref showRewardTitleOnNameplate))
                {
                    Plugin.Configuration.ShowRewardTitleOnNameplate = showRewardTitleOnNameplate;
                    Plugin.Configuration.Save();
                    Plugin.RefreshNameplateTitlePreview();
                }

                if (ImGui.BeginCombo("Reward Title Color", rewardTitleColorPreset))
                {
                    foreach (var option in titleColorOptions)
                    {
                        var unlocked = string.IsNullOrWhiteSpace(option.UnlockSecretTitle) || unlockedSecrets.Contains(option.UnlockSecretTitle);
                        var label = unlocked
                            ? option.Name
                            : $"{option.Name} (unlock: {option.UnlockSecretTitle})";

                        if (!unlocked)
                        {
                            ImGui.BeginDisabled();
                        }

                        var selected = rewardTitleColorPreset == option.Name;
                        if (ImGui.Selectable(label, selected) && unlocked)
                        {
                            rewardTitleColorPreset = option.Name;
                            Plugin.Configuration.RewardTitleColorPreset = option.Name;
                            Plugin.Configuration.Save();
                            Plugin.RefreshNameplateTitlePreview();
                        }

                        if (!unlocked)
                        {
                            ImGui.EndDisabled();
                        }
                    }

                    ImGui.EndCombo();
                }
            }

            if (ImGui.CollapsingHeader("Title Sync (Phase 1)"))
            {
                if (ImGui.Checkbox("Enable Title Sync", ref enableTitleSync))
                {
                    Plugin.Configuration.EnableTitleSync = enableTitleSync;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Checkbox("Share My Title", ref shareTitleSync))
                {
                    Plugin.Configuration.ShareTitleSync = shareTitleSync;
                    Plugin.Configuration.Save();
                    Plugin.RequestTitleSyncNow();
                }

                if (ImGui.Checkbox("Show Synced Titles", ref showSyncedTitles))
                {
                    Plugin.Configuration.ShowSyncedTitles = showSyncedTitles;
                    Plugin.Configuration.Save();
                    Plugin.RefreshNameplateTitlePreview();
                }

                if (ImGui.Button("Push Title Now"))
                {
                    Plugin.RequestTitleSyncNow();
                }

                ImGui.TextDisabled($"Sync service: {Plugin.Configuration.TitleSyncApiUrl}");
                ImGui.TextDisabled("No API key setup required for users.");
            }

            if (ImGui.CollapsingHeader("Daily Quests"))
            {
                ImGui.TextWrapped("Preset filters the random pool: Solo is general social quests, Party adds group/combat-flavored emote quests, and RP prioritizes roleplay-themed social quests.");
                if (ImGui.BeginCombo("Preset", presetSelection))
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
                    }

                    ImGui.EndCombo();
                }

                if (ImGui.Button("Re-roll Daily Quests Now"))
                {
                    QuestManager.ForceRerollDailyQuests(DateTime.Now);
                    packStatusMessage = "Daily quests re-rolled for current preset.";
                }
            }

            if (ImGui.CollapsingHeader("Tools"))
            {
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
                if (ImGui.Button("Test Quest Complete Sound"))
                {
                    Plugin.TestQuestCompleteSound();
                }

                ImGui.SameLine();
                if (ImGui.Button("Test Level Up Sound"))
                {
                    Plugin.TestLevelUpSound();
                }

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
            }

            if (ImGui.CollapsingHeader("Analytics"))
            {
                var stats = QuestManager.GetStats();

                if (ImGui.BeginTable("##AnalyticsSummary", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    DrawStatRow("Unlocked Title", stats.UnlockedTitle);
                    DrawStatRow("Weekly Rank", stats.WeeklyRank);
                    DrawStatRow("Total Completions", stats.TotalCompletions.ToString());
                    DrawStatRow("Total Progress Ticks", stats.TotalProgressTicks.ToString());
                    DrawStatRow("Current Streak", $"{stats.CurrentStreakDays} day(s)");
                    DrawStatRow("Best Streak", $"{stats.BestStreakDays} day(s)");
                    DrawStatRow("Weekly Completions", stats.WeeklyCompletions.ToString());
                    ImGui.EndTable();
                }

                ImGui.Spacing();
                ImGui.Text("Secret Titles");
                const int secretColumns = 3;
                if (ImGui.BeginTable("##SecretTitleGrid", secretColumns, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    for (var i = 0; i < secretProgress.Count; i++)
                    {
                        if (i % secretColumns == 0)
                        {
                            ImGui.TableNextRow();
                        }

                        ImGui.TableNextColumn();
                        var entry = secretProgress[i];
                        ImGui.TextUnformatted(entry.Title);
                        ImGui.TextDisabled($"{entry.CurrentCount}/{entry.Requirement}");
                        ImGui.TextUnformatted(entry.Unlocked ? "Unlocked" : "Locked");
                    }

                    ImGui.EndTable();
                }

                if (stats.RecentDailyCompletions.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text("Last 14 Days");
                    if (ImGui.BeginTable("##RecentDays", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Date");
                        ImGui.TableSetupColumn("Completions");
                        ImGui.TableHeadersRow();

                        foreach (var day in stats.RecentDailyCompletions)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(day.Date);
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(day.Count.ToString());
                        }

                        ImGui.EndTable();
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private static void DrawStatRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
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

public readonly struct RewardColorOption
{
    public RewardColorOption(string name, string? unlockSecretTitle)
    {
        Name = name;
        UnlockSecretTitle = unlockSecretTitle;
    }

    public string Name { get; }
    public string? UnlockSecretTitle { get; }
}

