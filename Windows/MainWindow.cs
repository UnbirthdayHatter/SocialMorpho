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
        var reputation = QuestManager.GetReputationProgress();

        ImGui.TextUnformatted($"Title: {progress.CurrentTitle}");
        ImGui.SameLine();
        ImGui.TextDisabled($"Next: {progress.NextTitle}");

        var needed = progress.NextRequirement;
        var pct = needed <= 0 ? 1f : Math.Clamp((float)progress.CurrentCompletions / needed, 0f, 1f);
        ImGui.ProgressBar(pct, new Vector2(-1f, 18f), $"{progress.CurrentCompletions}/{needed}");
        ImGui.TextDisabled($"{progress.RemainingToNext} completion(s) to next title");

        ImGui.Spacing();
        ImGui.TextUnformatted($"Reputation: {reputation.CurrentRank}");
        ImGui.SameLine();
        ImGui.TextDisabled($"Next: {reputation.NextRank}");
        var repSpan = Math.Max(1, reputation.NextRankXp - reputation.CurrentRankXpFloor);
        var repProgress = reputation.NextRank == "Max rank reached"
            ? 1f
            : Math.Clamp((float)(reputation.CurrentXp - reputation.CurrentRankXpFloor) / repSpan, 0f, 1f);
        ImGui.ProgressBar(repProgress, new Vector2(-1f, 16f), $"{reputation.CurrentXp} XP");
        ImGui.TextDisabled($"Next reward: {reputation.NextRewardTitle} + {reputation.NextRewardStyle}");
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
        ImGui.Text($"Anti-Cheese Risk: {quest.AntiCheeseRisk}");

        var risk = quest.AntiCheeseRisk;
        if (ImGui.BeginCombo("Risk Class", risk.ToString()))
        {
            foreach (var option in Enum.GetValues<AntiCheeseRisk>())
            {
                var selected = risk == option;
                if (ImGui.Selectable(option.ToString(), selected))
                {
                    quest.AntiCheeseRisk = option;
                    Plugin.Configuration.Save();
                }
            }
            ImGui.EndCombo();
        }
        
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
        bool enablePerTitleStyleProfiles = Plugin.Configuration.EnablePerTitleStyleProfiles;
        bool forceSocialMorphoTitleColors = Plugin.Configuration.ForceSocialMorphoTitleColors;
        bool enableTitleSync = Plugin.Configuration.EnableTitleSync;
        bool shareTitleSync = Plugin.Configuration.ShareTitleSync;
        bool showSyncedTitles = Plugin.Configuration.ShowSyncedTitles;
        bool enableCloudLeaderboard = Plugin.Configuration.EnableCloudLeaderboard;
        bool shareCloudLeaderboardStats = Plugin.Configuration.ShareCloudLeaderboardStats;
        bool showCloudLeaderboard = Plugin.Configuration.ShowCloudLeaderboard;
        bool hideWorldOnCloudLeaderboard = Plugin.Configuration.HideWorldOnCloudLeaderboard;
        var cloudLeaderboardAlias = Plugin.Configuration.CloudLeaderboardAlias ?? string.Empty;
        bool enableQuestChains = Plugin.Configuration.EnableQuestChains;
        bool enableDuoSynergy = Plugin.Configuration.EnableDuoSynergy;
        var duoPartnerName = Plugin.Configuration.DuoPartnerName ?? string.Empty;
        var antiCheeseTier = string.IsNullOrWhiteSpace(Plugin.Configuration.AntiCheeseTier) ? "Balanced" : Plugin.Configuration.AntiCheeseTier;
        var rewardTitleColorPreset = Plugin.Configuration.RewardTitleColorPreset;
        var selectedStarterTitle = Plugin.Configuration.SelectedStarterTitle;
        var presetOptions = new[] { "Solo", "Party", "RP" };
        var starterTitleOptions = new[] { "New Adventurer", "Wandering Soul", "Friendly Face", "Lantern Smile", "City Sprout" };
        var titleProgress = QuestManager.GetTitleProgress();
        var stats = QuestManager.GetStats();
        var secretProgress = QuestManager.GetSecretTitleProgress();
        var unlockedSecrets = new HashSet<string>(secretProgress.Where(s => s.Unlocked).Select(s => s.Title), StringComparer.Ordinal);
        foreach (var seasonalTitle in Plugin.Configuration.UnlockedSeasonalTitles)
        {
            unlockedSecrets.Add(seasonalTitle);
        }
        foreach (var repReward in Plugin.Configuration.UnlockedReputationRewards)
        {
            unlockedSecrets.Add(repReward);
        }

        var titleColorOptions = new[]
        {
            new RewardColorOption("Gold", null),
            new RewardColorOption("Pink", null),
            new RewardColorOption("Cyan", null),
            new RewardColorOption("Rose", null),
            new RewardColorOption("Mint", null),
            new RewardColorOption("Violet", null),
            new RewardColorOption("Gold Gradient", null),
            new RewardColorOption("Rose Gradient", null),
            new RewardColorOption("Cyan Gradient", null),
            new RewardColorOption("Violet Gradient", null),
            new RewardColorOption("Sunset Gradient", null),
            new RewardColorOption("Seafoam Gradient", null),
            new RewardColorOption("Valentione Gradient", "Rosebound Courier"),
            new RewardColorOption("Companion Gradient", "Companion of Hearts"),
            new RewardColorOption("Confidant Gradient", "Trusted Confidant"),
            new RewardColorOption("Muse Gradient", "Midnight Muse"),
            new RewardColorOption("Heartbound Gradient", "Heartbound Legend"),
            new RewardColorOption("Rainbow Gradient", "The Perfect Legend"),
            new RewardColorOption("Lily Gradient", "Butterfly Kisses"),
            new RewardColorOption("Royal Gradient", "Boogie Master"),
            new RewardColorOption("Aurora Gradient", "Daily Grinder"),
            new RewardColorOption("Peach Fizz Gradient", "Kiss Collector"),
            new RewardColorOption("Moonlit Gradient", "Victory Lap"),
            new RewardColorOption("Ember Gradient", "Battle Ready"),
            new RewardColorOption("Ocean Dusk Gradient", "Wave Whisperer"),
            new RewardColorOption("Spring Bloom Gradient", "Hug Magnet"),
            new RewardColorOption("Crystal Sky Gradient", "Guiding Light"),
            new RewardColorOption("Stardust Gradient", "Take a Chance On Me"),
            new RewardColorOption("Festival Gradient", "Party Animal"),
            new RewardColorOption("Scholar Gradient", "Peer Reviewed"),
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

                if (stats.TotalCompletions < 10)
                {
                    if (ImGui.BeginCombo("Starter Title", selectedStarterTitle))
                    {
                        foreach (var option in starterTitleOptions)
                        {
                            var selected = selectedStarterTitle == option;
                            if (ImGui.Selectable(option, selected))
                            {
                                selectedStarterTitle = option;
                                Plugin.Configuration.SelectedStarterTitle = option;
                                Plugin.Configuration.Stats.UnlockedTitle = option;
                                Plugin.Configuration.Save();
                                Plugin.RefreshNameplateTitlePreview();
                            }
                        }

                        ImGui.EndCombo();
                    }
                    ImGui.TextDisabled("Available until 10 completions.");
                }
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

                if (ImGui.Checkbox("Force SocialMorpho Title Colors", ref forceSocialMorphoTitleColors))
                {
                    Plugin.Configuration.ForceSocialMorphoTitleColors = forceSocialMorphoTitleColors;
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

                if (ImGui.Checkbox("Enable Per-Title Style Profiles", ref enablePerTitleStyleProfiles))
                {
                    Plugin.Configuration.EnablePerTitleStyleProfiles = enablePerTitleStyleProfiles;
                    Plugin.Configuration.Save();
                    Plugin.RefreshNameplateTitlePreview();
                }

                var currentUnlocked = string.IsNullOrWhiteSpace(stats.UnlockedTitle) ? "New Adventurer" : stats.UnlockedTitle;
                if (ImGui.Button("Save Current Style To Current Title"))
                {
                    Plugin.Configuration.TitleStyleProfiles[currentUnlocked] = rewardTitleColorPreset;
                    Plugin.Configuration.Save();
                    Plugin.RefreshNameplateTitlePreview();
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear Current Title Style"))
                {
                    Plugin.Configuration.TitleStyleProfiles.Remove(currentUnlocked);
                    Plugin.Configuration.Save();
                    Plugin.RefreshNameplateTitlePreview();
                }
                ImGui.TextDisabled($"Current title profile target: {currentUnlocked}");
                if (Plugin.Configuration.TitleStyleProfiles.TryGetValue(currentUnlocked, out var mappedStyle) && !string.IsNullOrWhiteSpace(mappedStyle))
                {
                    ImGui.TextDisabled($"Mapped style: {mappedStyle}");
                }
            }

            if (ImGui.CollapsingHeader("Title Sync (Phase 1)"))
            {
                ImGui.TextDisabled("Honorific/Lightless fallback: always enabled");

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

                ImGui.Separator();
                ImGui.TextUnformatted("Cloud Leaderboard (Opt-In)");
                ImGui.TextDisabled("Optional cross-player board using total completions and reputation.");

                if (ImGui.Checkbox("Enable Cloud Leaderboard", ref enableCloudLeaderboard))
                {
                    Plugin.Configuration.EnableCloudLeaderboard = enableCloudLeaderboard;
                    Plugin.Configuration.Save();
                    if (enableCloudLeaderboard)
                    {
                        Plugin.RequestCloudLeaderboardRefresh();
                    }
                }

                if (ImGui.Checkbox("Share My Stats To Cloud Leaderboard", ref shareCloudLeaderboardStats))
                {
                    Plugin.Configuration.ShareCloudLeaderboardStats = shareCloudLeaderboardStats;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Checkbox("Show Cloud Leaderboard", ref showCloudLeaderboard))
                {
                    Plugin.Configuration.ShowCloudLeaderboard = showCloudLeaderboard;
                    Plugin.Configuration.Save();
                    if (showCloudLeaderboard)
                    {
                        Plugin.RequestCloudLeaderboardRefresh();
                    }
                }

                if (ImGui.InputText("Leaderboard Alias (optional)", ref cloudLeaderboardAlias, 32))
                {
                    Plugin.Configuration.CloudLeaderboardAlias = cloudLeaderboardAlias.Trim();
                    Plugin.Configuration.Save();
                }

                if (ImGui.Checkbox("Hide World On Cloud Leaderboard", ref hideWorldOnCloudLeaderboard))
                {
                    Plugin.Configuration.HideWorldOnCloudLeaderboard = hideWorldOnCloudLeaderboard;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Button("Refresh Cloud Leaderboard"))
                {
                    Plugin.RequestCloudLeaderboardRefresh();
                }

                ImGui.TextDisabled($"Active provider: {Plugin.GetTitleSyncProviderLabel()}");
                ImGui.TextDisabled("Cloud sync is primary. Honorific/Lightless activates after repeated cloud failures.");

                ImGui.Spacing();
                ImGui.Text("Sync Health");
                var health = Plugin.GetTitleSyncHealth();
                if (ImGui.BeginTable("##SyncHealthTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    DrawStatRow("Provider", health.ActiveProvider);
                    DrawStatRow("Honorific Detected", health.HonorificDetected ? "Yes" : "No");
                    DrawStatRow("Fallback Active", health.IsFallbackActive ? "Yes" : "No");
                    DrawStatRow("Consecutive Cloud Failures", health.ConsecutiveCloudFailures.ToString());
                    DrawStatRow("Nearby Players (Last Scan)", health.NearbyPlayersLastScan.ToString());
                    DrawStatRow("Cache Entries", health.CacheEntries.ToString());
                    DrawStatRow("Last Cloud Pull", health.LastCloudPullSuccessUtc == DateTime.MinValue ? "-" : health.LastCloudPullSuccessUtc.ToLocalTime().ToString("HH:mm:ss"));
                    DrawStatRow("Last Cloud Push", health.LastCloudPushSuccessUtc == DateTime.MinValue ? "-" : health.LastCloudPushSuccessUtc.ToLocalTime().ToString("HH:mm:ss"));
                    DrawStatRow("Last Honorific Pull", health.LastHonorificPullUtc == DateTime.MinValue ? "-" : health.LastHonorificPullUtc.ToLocalTime().ToString("HH:mm:ss"));
                    DrawStatRow("Last Honorific Push", health.LastHonorificPushUtc == DateTime.MinValue ? "-" : health.LastHonorificPushUtc.ToLocalTime().ToString("HH:mm:ss"));
                    DrawStatRow("Last Error", string.IsNullOrWhiteSpace(health.LastErrorSummary) ? "-" : health.LastErrorSummary);
                    ImGui.EndTable();
                }

                if (Plugin.Configuration.EnableCloudLeaderboard && Plugin.Configuration.ShowCloudLeaderboard)
                {
                    ImGui.Spacing();
                    ImGui.TextUnformatted("Global Top (Cloud)");
                    var cloudLeaders = Plugin.GetCloudLeaderboardSnapshot(25);
                    if (cloudLeaders.Count == 0)
                    {
                        ImGui.TextDisabled("No cloud leaderboard entries yet.");
                    }
                    else if (ImGui.BeginTable("##CloudLeaderboard", 5, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
                        ImGui.TableSetupColumn("Player");
                        ImGui.TableSetupColumn("Title");
                        ImGui.TableSetupColumn("Completions");
                        ImGui.TableSetupColumn("Rep XP");
                        ImGui.TableHeadersRow();

                        for (var i = 0; i < cloudLeaders.Count; i++)
                        {
                            var entry = cloudLeaders[i];
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted((i + 1).ToString());
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(entry.DisplayName);
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(entry.Title);
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(entry.TotalCompletions.ToString());
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(entry.ReputationXp.ToString());
                        }

                        ImGui.EndTable();
                    }
                }
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

                if (ImGui.Checkbox("Enable Daily Quest Chains", ref enableQuestChains))
                {
                    Plugin.Configuration.EnableQuestChains = enableQuestChains;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Checkbox("Enable Duo Synergy Quests", ref enableDuoSynergy))
                {
                    Plugin.Configuration.EnableDuoSynergy = enableDuoSynergy;
                    Plugin.Configuration.Save();
                }

                if (ImGui.InputText("Duo Partner Name", ref duoPartnerName, 64))
                {
                    Plugin.Configuration.DuoPartnerName = duoPartnerName.Trim();
                    Plugin.Configuration.Save();
                }

                var antiCheeseOptions = new[] { "Relaxed", "Balanced", "Strict" };
                if (ImGui.BeginCombo("Anti-Cheese Cooldown Tier", antiCheeseTier))
                {
                    foreach (var option in antiCheeseOptions)
                    {
                        var selected = antiCheeseTier == option;
                        if (ImGui.Selectable(option, selected))
                        {
                            antiCheeseTier = option;
                            Plugin.Configuration.AntiCheeseTier = option;
                            Plugin.Configuration.Save();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.TextDisabled($"Anti-cheese status: {QuestManager.GetAntiCheeseStatus()}");
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
                if (ImGui.Button("Open Quest Board"))
                {
                    Plugin.OpenQuestBoard();
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

                ImGui.Spacing();
                ImGui.Text("Nearby Friends Leaderboard (Session)");
                var synced = Plugin.GetRankedSyncedTitleSnapshot(24);
                if (synced.Count == 0)
                {
                    ImGui.TextDisabled("No synced nearby players yet.");
                }
                else if (ImGui.BeginTable("##SyncedLeaderboard", 5, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
                    ImGui.TableSetupColumn("Player");
                    ImGui.TableSetupColumn("Title");
                    ImGui.TableSetupColumn("Style");
                    ImGui.TableSetupColumn("Seen");
                    ImGui.TableHeadersRow();

                    for (var i = 0; i < synced.Count; i++)
                    {
                        var entry = synced[i];
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted((i + 1).ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(entry.Character);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(entry.Title);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(entry.Style) ? "Gold" : entry.Style);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(entry.SeenCount.ToString());
                    }

                    ImGui.EndTable();
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

