using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SocialMorpho.Data;

public class QuestManager
{
    private const ulong DailyQuestIdBase = 900000;
    private static readonly TimeSpan DuplicateChatDebounce = TimeSpan.FromSeconds(2);
    private static readonly Dictionary<string, TimeSpan> EventCooldowns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["duty_completion"] = TimeSpan.FromSeconds(20),
        ["helping_hand"] = TimeSpan.FromSeconds(20),
        ["wonderous_friend"] = TimeSpan.FromSeconds(20),
        ["party_join"] = TimeSpan.FromSeconds(60),
        ["commendation"] = TimeSpan.FromSeconds(10),
        ["housing_entered"] = TimeSpan.FromSeconds(30),
        ["dote"] = TimeSpan.FromSeconds(2),
        ["hug"] = TimeSpan.FromSeconds(2),
        ["wave"] = TimeSpan.FromSeconds(2),
        ["dance"] = TimeSpan.FromSeconds(2),
        ["cheer"] = TimeSpan.FromSeconds(2),
        ["bow"] = TimeSpan.FromSeconds(2),
        ["salute"] = TimeSpan.FromSeconds(2),
        ["thumbsup"] = TimeSpan.FromSeconds(2),
        ["blowkiss"] = TimeSpan.FromSeconds(2),
        ["battlestance"] = TimeSpan.FromSeconds(2),
        ["victory"] = TimeSpan.FromSeconds(2),
        ["spectacles"] = TimeSpan.FromSeconds(2),
    };
    private static readonly List<TitleTier> BaseTitleTiers = new()
    {
        new TitleTier("New Adventurer", 0),
        new TitleTier("Budding Friend", 10),
        new TitleTier("Social Star", 30),
        new TitleTier("Heart of Eorzea", 75),
    };

    private static readonly List<SecretTitleTier> SecretTitleTiers = new()
    {
        new SecretTitleTier("Butterfly Kisses", "dote", 100),
        new SecretTitleTier("Boogie Master", "dance", 50),
        new SecretTitleTier("Hug Magnet", "hug", 75),
        new SecretTitleTier("Kiss Collector", "blowkiss", 60),
        new SecretTitleTier("Wave Whisperer", "wave", 75),
        new SecretTitleTier("Sky Saluter", "salute", 50),
        new SecretTitleTier("Courtly Bow", "bow", 60),
        new SecretTitleTier("Crowd Favorite", "cheer", 60),
        new SecretTitleTier("Thumbs of Approval", "thumbsup", 60),
        new SecretTitleTier("Battle Ready", "battlestance", 40),
        new SecretTitleTier("Victory Lap", "victory", 40),
        new SecretTitleTier("Four Eyes", "spectacles", 40),
        new SecretTitleTier("Peer Reviewed", "commendation", 50),
        new SecretTitleTier("Daily Grinder", "duty_completion", 200),
        new SecretTitleTier("Guiding Light", "helping_hand", 50),
        new SecretTitleTier("Take a Chance On Me", "wonderous_friend", 100),
        new SecretTitleTier("Party Animal", "party_join", 200),
    };

    private readonly Configuration Configuration;
    private readonly Dictionary<ulong, QuestProgress> QuestProgress = new();
    private readonly Dictionary<string, DateTime> lastEventSeenUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> lastChatSeenUtc = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<DailySocialQuestTemplate> DailySocialQuestTemplates = new()
    {
        new DailySocialQuestTemplate("Sweet on You", "Have 3 different players use /dote on you", 3, new[] { "dotes on you", "dotes you" }, new[] { "Solo", "Party", "RP" }),
        new DailySocialQuestTemplate("Spread the Love", "Have 3 different players use /hug on you", 3, new[] { "hugs you" }, new[] { "Solo", "Party", "RP" }),
        new DailySocialQuestTemplate("Kiss Blown Your Way", "Have 3 different players use /blowkiss on you", 3, new[] { "blows you a kiss", "blow kisses you" }, new[] { "Solo", "RP" }),
        new DailySocialQuestTemplate("Dance Fever", "Have 3 different players use /dance with you", 3, new[] { "dances with you", "dances for you" }, new[] { "Party", "RP" }),
        new DailySocialQuestTemplate("Friendly Faces", "Have 3 different players use /wave to you", 3, new[] { "waves to you" }, new[] { "Solo", "Party", "RP" }),
        new DailySocialQuestTemplate("Hype Circle", "Have 3 different players use /cheer for you", 3, new[] { "cheers you on", "cheers for you" }, new[] { "Party", "RP" }),
        new DailySocialQuestTemplate("Bow Trio", "Have 3 different players use /bow to you", 3, new[] { "bows to you" }, new[] { "Solo", "RP" }),
        new DailySocialQuestTemplate("Respect Given", "Have 3 different players use /salute to you", 3, new[] { "salutes you" }, new[] { "Party", "RP" }),
        new DailySocialQuestTemplate("Good Vibes Only", "Have 3 different players use /thumbsup to you", 3, new[] { "gives you a thumbs up", "gives you the thumbs up" }, new[] { "Solo", "Party" }),
        new DailySocialQuestTemplate("Ready Check Vibes", "Have 3 different players use /battlestance near you", 3, new[] { "assumes a battle stance" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Victory Circle", "Have 3 different players use /victory with you", 3, new[] { "strikes a victory pose" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Four Eyes Club", "Have 3 different players use /spectacles at you", 3, new[] { "adjusts their spectacles" }, new[] { "RP" }),
        new DailySocialQuestTemplate("Encore Please", "Have 3 different players use /cheer for you", 3, new[] { "cheers you on", "cheers for you" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Polite Company", "Have 3 different players use /bow to you", 3, new[] { "bows to you" }, new[] { "RP" }),
        new DailySocialQuestTemplate("Fond Memories", "Receive 1 player commendation", 1, new[] { "commendation", "player commendation" }, new[] { "Solo", "Party", "RP" }),
        new DailySocialQuestTemplate("Duty Roulette", "Complete any duty", 1, new[] { "completion time" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Helping Hand", "Help a first-timer clear a duty", 1, new[] { "one or more party members completed this duty for the first time" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Wonderous Friend", "Join a duty with at least one player who has not cleared it yet", 1, new[] { "one or more party members have yet to complete this duty" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Squad Goals", "Join a party", 1, new[] { "joined the party", "joins the party" }, new[] { "Party" }),
        new DailySocialQuestTemplate("Home Sweet Home", "Enter a housing district or estate", 1, new[] { "entered housing" }, new[] { "Solo", "RP" }),
    };

    public QuestManager(Configuration configuration)
    {
        Configuration = configuration;
        EnsureStatsInitialized();
        LoadProgressFromConfig();
        EnsureStatsBuckets(DateTime.Now);
    }

    private void LoadProgressFromConfig()
    {
        foreach (var quest in Configuration.SavedQuests)
        {
            QuestProgress[quest.Id] = new QuestProgress { IsComplete = quest.Completed, Progress = quest.CurrentCount };
        }
    }

    public List<QuestData> GetAllQuests()
    {
        return Configuration.SavedQuests;
    }

    public QuestData? GetQuest(ulong questId)
    {
        return Configuration.SavedQuests.FirstOrDefault(q => q.Id == questId);
    }

    public void AddQuest(ulong questId, string questTitle)
    {
        if (!Configuration.SavedQuests.Any(q => q.Id == questId))
        {
            var quest = new QuestData { Id = questId, Title = questTitle };
            Configuration.SavedQuests.Add(quest);
            Configuration.Save();
        }
    }

    public void AddQuest(QuestData quest)
    {
        if (!Configuration.SavedQuests.Any(q => q.Id == quest.Id))
        {
            Configuration.SavedQuests.Add(quest);
            Configuration.Save();
        }
    }

    public void RemoveQuest(ulong questId)
    {
        var quest = Configuration.SavedQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            Configuration.SavedQuests.Remove(quest);
            Configuration.Save();
        }
    }

    public void MarkQuestComplete(ulong questId)
    {
        var quest = GetQuest(questId);
        if (quest != null && !quest.Completed)
        {
            var now = DateTime.Now;
            quest.Completed = true;
            quest.CompletedAt = now;
            RegisterCompletion(now);
            if (Configuration.CurrentDailyQuestIds.Contains(quest.Id))
            {
                ReplaceCompletedDailyQuest(quest.Id, now);
            }
            Configuration.Save();
        }
    }

    public void ResetQuestProgress(ulong questId)
    {
        var quest = GetQuest(questId);
        if (quest != null)
        {
            quest.Completed = false;
            quest.CurrentCount = 0;
            quest.CompletedAt = null;
            Configuration.Save();
        }
    }

    public void ResetAllQuestProgress()
    {
        foreach (var quest in Configuration.SavedQuests)
        {
            quest.Completed = false;
            quest.CurrentCount = 0;
            quest.CompletedAt = null;
        }
        Configuration.Save();
    }

    public void LoadQuestsFromJson(string configDirectory)
    {
        try
        {
            var jsonPath = Path.Combine(configDirectory, "Quests.json");
            if (!File.Exists(jsonPath))
            {
                return;
            }

            var jsonContent = File.ReadAllText(jsonPath);
            var questFile = JsonSerializer.Deserialize<QuestFile>(jsonContent);

            if (questFile?.Quests != null)
            {
                foreach (var quest in questFile.Quests)
                {
                    // Only add if not already present
                    if (!Configuration.SavedQuests.Any(q => q.Id == quest.Id))
                    {
                        Configuration.SavedQuests.Add(quest);
                    }
                }
                Configuration.Save();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - will be logged by IPluginLog in Plugin.cs
            Console.WriteLine($"Error loading quests from JSON: {ex.Message}");
        }
    }

    public void CheckAndResetQuests()
    {
        var now = DateTime.Now;
        bool anyReset = false;

        foreach (var quest in Configuration.SavedQuests)
        {
            if (quest.ResetSchedule == ResetSchedule.None)
                continue;

            bool shouldReset = false;

            if (quest.LastResetDate == null)
            {
                quest.LastResetDate = now;
                continue;
            }

            if (quest.ResetSchedule == ResetSchedule.Daily)
            {
                // Reset if last reset was on a different day
                if (quest.LastResetDate.Value.Date < now.Date)
                {
                    shouldReset = true;
                }
            }
            else if (quest.ResetSchedule == ResetSchedule.Weekly)
            {
                // Reset if last reset was in a different week (Monday as start of week)
                var daysSinceLastReset = (now.Date - quest.LastResetDate.Value.Date).Days;
                var lastResetDayOfWeek = (int)quest.LastResetDate.Value.DayOfWeek;
                var currentDayOfWeek = (int)now.DayOfWeek;

                // If more than 7 days, definitely reset
                if (daysSinceLastReset >= 7)
                {
                    shouldReset = true;
                }
                // If we've crossed a Monday
                else if (lastResetDayOfWeek > currentDayOfWeek || 
                         (lastResetDayOfWeek == 0 && currentDayOfWeek != 0 && daysSinceLastReset > 0))
                {
                    shouldReset = true;
                }
            }

            if (shouldReset)
            {
                quest.Completed = false;
                quest.CurrentCount = 0;
                quest.CompletedAt = null;
                quest.LastResetDate = now;
                anyReset = true;
            }
        }

        if (anyReset)
        {
            Configuration.Save();
        }
    }

    public QuestProgress? GetQuestProgress(ulong questId)
    {
        return QuestProgress.TryGetValue(questId, out var progress) ? progress : null;
    }

    public List<QuestData> GetActiveQuests()
    {
        var activeDaily = Configuration.CurrentDailyQuestIds
            .Select(id => Configuration.SavedQuests.FirstOrDefault(q => q.Id == id))
            .Where(q => q != null && !q.Completed)
            .Cast<QuestData>();

        var activeOther = Configuration.SavedQuests
            .Where(q => !q.Completed && !Configuration.CurrentDailyQuestIds.Contains(q.Id))
            .OrderByDescending(q => q.CreatedAt);

        return activeDaily.Concat(activeOther).Take(3).ToList();
    }

    public List<QuestData> GetCompletedQuests()
    {
        return Configuration.SavedQuests.Where(q => q.Completed).ToList();
    }

    public bool IncrementDoteQuestProgress()
    {
        return IncrementQuestProgressFromChatDetailed("dotes on you") != null;
    }

    public bool IncrementQuestProgressFromChat(string chatText)
    {
        return IncrementQuestProgressFromChatDetailed(chatText) != null;
    }

    public ProgressUpdateResult? IncrementQuestProgressFromChatDetailed(string chatText)
    {
        if (string.IsNullOrWhiteSpace(chatText))
        {
            return null;
        }

        var now = DateTime.Now;
        EnsureStatsBuckets(now);

        var normalized = chatText.Trim();
        var normalizedLower = normalized.ToLowerInvariant();
        if (IsDuplicateChatLine(normalizedLower, now))
        {
            return null;
        }

        var trackedEvent = DetectTrackedEvent(normalizedLower);
        if (!string.IsNullOrWhiteSpace(trackedEvent) && IsEventOnCooldown(trackedEvent, now))
        {
            return null;
        }

        var tracked = false;
        if (!string.IsNullOrWhiteSpace(trackedEvent))
        {
            tracked = TrackActivityEvent(trackedEvent);
        }

        var quest = Configuration.SavedQuests.FirstOrDefault(q =>
            !q.Completed &&
            IsQuestTriggeredByChat(q, normalized, normalizedLower));

        if (quest == null)
        {
            if (tracked)
            {
                MarkEventSeen(trackedEvent!, now);
                Configuration.Save();
            }
            return null;
        }

        var oldCount = quest.CurrentCount;
        quest.CurrentCount = Math.Min(quest.GoalCount, quest.CurrentCount + 1);
        var delta = Math.Max(0, quest.CurrentCount - oldCount);
        if (delta <= 0)
        {
            return null;
        }

        Configuration.Stats.TotalProgressTicks += delta;
        var completedNow = false;
        if (quest.CurrentCount >= quest.GoalCount)
        {
            quest.Completed = true;
            quest.CompletedAt = now;
            RegisterCompletion(now);
            completedNow = true;

            if (Configuration.CurrentDailyQuestIds.Contains(quest.Id))
            {
                ReplaceCompletedDailyQuest(quest.Id, now);
            }
        }

        Configuration.Stats.UnlockedTitle = GetUnlockedTitle(Configuration.Stats);
        if (!string.IsNullOrWhiteSpace(trackedEvent))
        {
            MarkEventSeen(trackedEvent, now);
        }

        Configuration.Save();
        return new ProgressUpdateResult
        {
            QuestId = quest.Id,
            QuestTitle = quest.Title,
            QuestType = quest.Type,
            Delta = delta,
            NewCount = quest.CurrentCount,
            GoalCount = quest.GoalCount,
            CompletedNow = completedNow,
        };
    }

    public ProgressUpdateResult? IncrementQuestProgressFromSystemEvent(string eventKey, string fallbackQuestText)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return null;
        }

        var now = DateTime.Now;
        if (IsEventOnCooldown(eventKey, now))
        {
            return null;
        }

        TrackActivityEvent(eventKey);
        MarkEventSeen(eventKey, now);

        var normalized = fallbackQuestText?.Trim() ?? string.Empty;
        var normalizedLower = normalized.ToLowerInvariant();
        var quest = Configuration.SavedQuests.FirstOrDefault(q =>
            !q.Completed &&
            IsQuestTriggeredByChat(q, normalized, normalizedLower));
        if (quest == null)
        {
            Configuration.Save();
            return null;
        }

        var oldCount = quest.CurrentCount;
        quest.CurrentCount = Math.Min(quest.GoalCount, quest.CurrentCount + 1);
        var delta = Math.Max(0, quest.CurrentCount - oldCount);
        if (delta <= 0)
        {
            Configuration.Save();
            return null;
        }

        Configuration.Stats.TotalProgressTicks += delta;
        var completedNow = false;
        if (quest.CurrentCount >= quest.GoalCount)
        {
            quest.Completed = true;
            quest.CompletedAt = now;
            RegisterCompletion(now);
            completedNow = true;

            if (Configuration.CurrentDailyQuestIds.Contains(quest.Id))
            {
                ReplaceCompletedDailyQuest(quest.Id, now);
            }
        }

        Configuration.Stats.UnlockedTitle = GetUnlockedTitle(Configuration.Stats);
        Configuration.Save();
        return new ProgressUpdateResult
        {
            QuestId = quest.Id,
            QuestTitle = quest.Title,
            QuestType = quest.Type,
            Delta = delta,
            NewCount = quest.CurrentCount,
            GoalCount = quest.GoalCount,
            CompletedNow = completedNow,
        };
    }

    private static bool IsQuestTriggeredByChat(QuestData quest, string chatText, string chatLower)
    {
        // Exact phrase support (existing behavior).
        if (quest.TriggerPhrases.Any(p => chatText.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Flexible emote detection: emote keyword(s) + "you".
        var emote = ExtractQuestEmote(quest);
        if (string.IsNullOrWhiteSpace(emote))
        {
            return false;
        }

        var hasYou = chatLower.Contains(" you");
        if (!hasYou && !chatLower.EndsWith("you", StringComparison.Ordinal))
        {
            return false;
        }

        return emote switch
        {
            "dote" => ContainsAny(chatLower, "dote", "dotes"),
            "hug" => ContainsAny(chatLower, "hug", "hugs"),
            "wave" => ContainsAny(chatLower, "wave", "waves"),
            "dance" => ContainsAny(chatLower, "dance", "dances"),
            "cheer" => ContainsAny(chatLower, "cheer", "cheers"),
            "bow" => ContainsAny(chatLower, "bow", "bows"),
            "salute" => ContainsAny(chatLower, "salute", "salutes"),
            "thumbsup" => ContainsAny(chatLower, "thumbsup", "thumbs up", "the thumbs up", "a thumbs up", "gives you a thumbs up"),
            "blowkiss" => ContainsAny(chatLower, "blowkiss", "blow kiss", "blows you a kiss", "blow kisses") || (chatLower.Contains("blow") && chatLower.Contains("kiss")),
            _ => ContainsAny(chatLower, emote, $"{emote}s"),
        };
    }

    private static string? ExtractQuestEmote(QuestData quest)
    {
        static string? GetFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var m = Regex.Match(text, @"/([a-z]+)", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            return m.Groups[1].Value.ToLowerInvariant();
        }

        return GetFromText(quest.Description) ?? GetFromText(quest.Title);
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (var value in values)
        {
            if (source.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void EnsureDailySocialQuests(DateTime now)
    {
        RefreshDailyQuestTitles();

        if (Configuration.LastDailyQuestSelectionDate?.Date == now.Date &&
            Configuration.CurrentDailyQuestIds.Count == 3 &&
            Configuration.CurrentDailyQuestIds.All(id => Configuration.SavedQuests.Any(q => q.Id == id)))
        {
            var changed = false;
            foreach (var questId in Configuration.CurrentDailyQuestIds.ToArray())
            {
                var quest = Configuration.SavedQuests.FirstOrDefault(q => q.Id == questId);
                if (quest == null || !quest.Completed)
                {
                    continue;
                }

                ReplaceCompletedDailyQuest(questId, now);
                changed = true;
            }

            if (changed)
            {
                Configuration.Save();
            }
            return;
        }

        RemovePreviousDailyQuests();

        var preset = string.IsNullOrWhiteSpace(Configuration.ActiveQuestPreset) ? "Solo" : Configuration.ActiveQuestPreset;
        var rng = new Random(now.Date.GetHashCode());
        var pool = DailySocialQuestTemplates
            .Where(t => t.Presets.Contains(preset, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (pool.Count < 3)
        {
            pool = DailySocialQuestTemplates.ToList();
        }

        var selectedTemplates = pool
            .OrderBy(_ => rng.Next())
            .Take(3)
            .ToList();

        var selectedQuestIds = new List<ulong>(selectedTemplates.Count);
        for (var i = 0; i < selectedTemplates.Count; i++)
        {
            var template = selectedTemplates[i];
            var questId = DailyQuestIdBase + (ulong)i;
            selectedQuestIds.Add(questId);

            Configuration.SavedQuests.Add(new QuestData
            {
                Id = questId,
                Title = template.Title,
                Description = template.Description,
                Type = QuestType.Social,
                GoalCount = template.GoalCount,
                CurrentCount = 0,
                Completed = false,
                CreatedAt = now,
                ResetSchedule = ResetSchedule.Daily,
                LastResetDate = now,
                TriggerPhrases = template.TriggerPhrases.ToList(),
            });
        }

        Configuration.LastDailyQuestSelectionDate = now.Date;
        Configuration.CurrentDailyQuestIds = selectedQuestIds;
        Configuration.Save();
    }

    private void ReplaceCompletedDailyQuest(ulong questId, DateTime now)
    {
        var quest = Configuration.SavedQuests.FirstOrDefault(q => q.Id == questId);
        if (quest == null)
        {
            return;
        }

        var preset = string.IsNullOrWhiteSpace(Configuration.ActiveQuestPreset) ? "Solo" : Configuration.ActiveQuestPreset;
        var pool = DailySocialQuestTemplates
            .Where(t => t.Presets.Contains(preset, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (pool.Count == 0)
        {
            pool = DailySocialQuestTemplates.ToList();
        }

        var usedDescriptions = Configuration.CurrentDailyQuestIds
            .Select(id => Configuration.SavedQuests.FirstOrDefault(q => q.Id == id))
            .Where(q => q != null && q.Id != questId)
            .Select(q => q!.Description)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var available = pool.Where(t => !usedDescriptions.Contains(t.Description)).ToList();
        if (available.Count == 0)
        {
            available = pool;
        }

        var template = available[Random.Shared.Next(available.Count)];
        quest.Title = template.Title;
        quest.Description = template.Description;
        quest.Type = QuestType.Social;
        quest.GoalCount = template.GoalCount;
        quest.CurrentCount = 0;
        quest.Completed = false;
        quest.CompletedAt = null;
        quest.CreatedAt = now;
        quest.ResetSchedule = ResetSchedule.Daily;
        quest.LastResetDate = now;
        quest.TriggerPhrases = template.TriggerPhrases.ToList();
    }

    public (bool success, string message) ExportQuestPack(string configDirectory)
    {
        try
        {
            var exportPath = Path.Combine(configDirectory, "QuestPack.export.json");
            var quests = Configuration.SavedQuests
                .Where(q => !Configuration.CurrentDailyQuestIds.Contains(q.Id))
                .ToList();

            var pack = new QuestPackFile
            {
                Name = "SocialMorpho Quest Pack",
                ExportedAt = DateTime.UtcNow.ToString("O"),
                Quests = quests,
            };

            var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(exportPath, json);
            return (true, $"Exported {quests.Count} quest(s) to {exportPath}");
        }
        catch (Exception ex)
        {
            return (false, $"Export failed: {ex.Message}");
        }
    }

    public (bool success, string message) ImportQuestPack(string configDirectory)
    {
        try
        {
            var importPath = Path.Combine(configDirectory, "QuestPack.import.json");
            if (!File.Exists(importPath))
            {
                return (false, $"Import file not found: {importPath}");
            }

            var json = File.ReadAllText(importPath);
            var pack = JsonSerializer.Deserialize<QuestPackFile>(json);
            if (pack?.Quests == null || pack.Quests.Count == 0)
            {
                return (false, "Import file has no quests.");
            }

            var added = 0;
            var skipped = 0;
            var nextId = Configuration.SavedQuests.Count > 0 ? Configuration.SavedQuests.Max(q => q.Id) + 1UL : 100UL;
            foreach (var q in pack.Quests)
            {
                if (string.IsNullOrWhiteSpace(q.Title) || q.GoalCount <= 0)
                {
                    skipped++;
                    continue;
                }

                if (Configuration.SavedQuests.Any(existing => existing.Id == q.Id))
                {
                    q.Id = nextId++;
                }

                q.CurrentCount = 0;
                q.Completed = false;
                q.CompletedAt = null;
                Configuration.SavedQuests.Add(q);
                added++;
            }

            Configuration.Save();
            return (true, $"Imported {added} quest(s), skipped {skipped}. Source: {importPath}");
        }
        catch (Exception ex)
        {
            return (false, $"Import failed: {ex.Message}");
        }
    }

    public void ForceRerollDailyQuests(DateTime now)
    {
        Configuration.LastDailyQuestSelectionDate = null;
        EnsureDailySocialQuests(now);
    }

    public SocialStats GetStats()
    {
        EnsureStatsBuckets(DateTime.Now);
        return Configuration.Stats;
    }

    public TitleProgressInfo GetTitleProgress()
    {
        var stats = GetStats();
        var currentCompletions = stats.TotalCompletions;
        var currentTier = BaseTitleTiers
            .Where(t => currentCompletions >= t.RequiredCompletions)
            .OrderByDescending(t => t.RequiredCompletions)
            .FirstOrDefault() ?? BaseTitleTiers[0];

        var nextTier = BaseTitleTiers
            .Where(t => t.RequiredCompletions > currentCompletions)
            .OrderBy(t => t.RequiredCompletions)
            .FirstOrDefault();

        if (nextTier == null)
        {
            return new TitleProgressInfo
            {
                CurrentTitle = currentTier.Title,
                NextTitle = "Max rank reached",
                CurrentCompletions = currentCompletions,
                NextRequirement = currentTier.RequiredCompletions,
                RemainingToNext = 0,
            };
        }

        return new TitleProgressInfo
        {
            CurrentTitle = currentTier.Title,
            NextTitle = nextTier.Title,
            CurrentCompletions = currentCompletions,
            NextRequirement = nextTier.RequiredCompletions,
            RemainingToNext = Math.Max(0, nextTier.RequiredCompletions - currentCompletions),
        };
    }

    public List<SecretTitleProgressInfo> GetSecretTitleProgress()
    {
        var stats = GetStats();
        var result = new List<SecretTitleProgressInfo>(SecretTitleTiers.Count);
        foreach (var tier in SecretTitleTiers)
        {
            var count = GetEmoteCount(stats, tier.EmoteKey);
            result.Add(new SecretTitleProgressInfo
            {
                Title = tier.Title,
                EmoteKey = tier.EmoteKey,
                CurrentCount = count,
                Requirement = tier.RequiredCount,
                Unlocked = count >= tier.RequiredCount,
            });
        }

        return result;
    }

    private void RegisterCompletion(DateTime now)
    {
        EnsureStatsBuckets(now);

        var stats = Configuration.Stats;
        stats.TotalCompletions++;
        stats.WeeklyCompletions++;
        stats.WeeklyRank = GetWeeklyRank(stats.WeeklyCompletions);
        stats.UnlockedTitle = GetUnlockedTitle(stats);

        if (stats.LastCompletionDate.HasValue)
        {
            var diff = (now.Date - stats.LastCompletionDate.Value.Date).Days;
            if (diff == 0)
            {
                // same-day completion does not change streak day count
            }
            else if (diff == 1)
            {
                stats.CurrentStreakDays++;
            }
            else
            {
                stats.CurrentStreakDays = 1;
            }
        }
        else
        {
            stats.CurrentStreakDays = 1;
        }

        if (stats.CurrentStreakDays > stats.BestStreakDays)
        {
            stats.BestStreakDays = stats.CurrentStreakDays;
        }

        stats.LastCompletionDate = now;

        var todayKey = now.ToString("yyyy-MM-dd");
        var todayEntry = stats.RecentDailyCompletions.FirstOrDefault(e => e.Date == todayKey);
        if (todayEntry == null)
        {
            stats.RecentDailyCompletions.Add(new DailyCompletionEntry { Date = todayKey, Count = 1 });
        }
        else
        {
            todayEntry.Count++;
        }

        // Keep last 14 days for lightweight local analytics.
        stats.RecentDailyCompletions = stats.RecentDailyCompletions
            .OrderByDescending(e => e.Date)
            .Take(14)
            .OrderBy(e => e.Date)
            .ToList();
    }

    private void EnsureStatsBuckets(DateTime now)
    {
        var stats = Configuration.Stats;
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        if (stats.WeeklyBucketStart != monday)
        {
            stats.WeeklyBucketStart = monday;
            stats.WeeklyCompletions = 0;
            stats.WeeklyRank = GetWeeklyRank(0);
        }
    }

    private static string GetWeeklyRank(int weeklyCompletions)
    {
        if (weeklyCompletions >= 10) return "Golden Heart";
        if (weeklyCompletions >= 5) return "Silver Socialite";
        if (weeklyCompletions >= 1) return "Bronze Butterfly";
        return "Sproutling";
    }

    private void EnsureStatsInitialized()
    {
        var stats = Configuration.Stats;
        stats.EmoteReceivedCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var secret in SecretTitleTiers)
        {
            if (!stats.EmoteReceivedCounts.ContainsKey(secret.EmoteKey))
            {
                stats.EmoteReceivedCounts[secret.EmoteKey] = 0;
            }
        }
    }

    private bool TrackActivityEvent(string eventKey)
    {
        EnsureStatsInitialized();
        var counts = Configuration.Stats.EmoteReceivedCounts;
        counts.TryGetValue(eventKey, out var current);
        counts[eventKey] = current + 1;
        Configuration.Stats.UnlockedTitle = GetUnlockedTitle(Configuration.Stats);
        return true;
    }

    private bool IsDuplicateChatLine(string normalizedLower, DateTime now)
    {
        if (this.lastChatSeenUtc.TryGetValue(normalizedLower, out var last) &&
            now - last <= DuplicateChatDebounce)
        {
            return true;
        }

        this.lastChatSeenUtc[normalizedLower] = now;
        return false;
    }

    private bool IsEventOnCooldown(string eventKey, DateTime now)
    {
        var cooldown = EventCooldowns.TryGetValue(eventKey, out var specific)
            ? specific
            : TimeSpan.FromSeconds(2);

        if (this.lastEventSeenUtc.TryGetValue(eventKey, out var last) &&
            now - last <= cooldown)
        {
            return true;
        }

        return false;
    }

    private void MarkEventSeen(string eventKey, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return;
        }

        this.lastEventSeenUtc[eventKey] = now;
    }

    private static string? DetectTrackedEvent(string chatLower)
    {
        if (string.IsNullOrWhiteSpace(chatLower))
        {
            return null;
        }

        if (chatLower.Contains("completion time", StringComparison.OrdinalIgnoreCase)) return "duty_completion";
        if (chatLower.Contains("one or more party members completed this duty for the first time", StringComparison.OrdinalIgnoreCase)) return "helping_hand";
        if (chatLower.Contains("one or more party members have yet to complete this duty", StringComparison.OrdinalIgnoreCase)) return "wonderous_friend";
        if (ContainsAny(chatLower, "you have joined the party", "joined the party", "joins the party")) return "party_join";
        if (chatLower.Contains("commendation", StringComparison.OrdinalIgnoreCase)) return "commendation";

        var hasYou = chatLower.Contains(" you", StringComparison.Ordinal) ||
                     chatLower.EndsWith("you", StringComparison.Ordinal);
        if (!hasYou)
        {
            return null;
        }

        if (ContainsAny(chatLower, "dote", "dotes")) return "dote";
        if (ContainsAny(chatLower, "blowkiss", "blow kiss", "blows you a kiss", "blow kisses")) return "blowkiss";
        if (ContainsAny(chatLower, "dance", "dances")) return "dance";
        if (ContainsAny(chatLower, "thumbsup", "thumbs up", "the thumbs up", "a thumbs up")) return "thumbsup";
        if (ContainsAny(chatLower, "salute", "salutes")) return "salute";
        if (ContainsAny(chatLower, "cheer", "cheers")) return "cheer";
        if (ContainsAny(chatLower, "wave", "waves")) return "wave";
        if (ContainsAny(chatLower, "hug", "hugs")) return "hug";
        if (ContainsAny(chatLower, "bow", "bows")) return "bow";
        if (ContainsAny(chatLower, "battle stance", "battlestance", "assumes a battle stance")) return "battlestance";
        if (ContainsAny(chatLower, "victory pose", "victory")) return "victory";
        if (ContainsAny(chatLower, "spectacles", "adjusts their spectacles")) return "spectacles";

        return null;
    }

    private static string GetUnlockedTitle(SocialStats stats)
    {
        foreach (var secret in SecretTitleTiers)
        {
            if (GetEmoteCount(stats, secret.EmoteKey) >= secret.RequiredCount)
            {
                return secret.Title;
            }
        }

        var totalCompletions = stats.TotalCompletions;
        if (totalCompletions >= 75) return "Heart of Eorzea";
        if (totalCompletions >= 30) return "Social Star";
        if (totalCompletions >= 10) return "Budding Friend";
        return "New Adventurer";
    }

    private static int GetEmoteCount(SocialStats stats, string emoteKey)
    {
        if (stats.EmoteReceivedCounts == null)
        {
            return 0;
        }

        return stats.EmoteReceivedCounts.TryGetValue(emoteKey, out var count) ? count : 0;
    }

    private void RefreshDailyQuestTitles()
    {
        var changed = false;
        foreach (var questId in Configuration.CurrentDailyQuestIds)
        {
            var quest = Configuration.SavedQuests.FirstOrDefault(q => q.Id == questId);
            if (quest == null)
            {
                continue;
            }

            var template = DailySocialQuestTemplates.FirstOrDefault(t =>
                t.Description.Equals(quest.Description, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                continue;
            }

            if (!quest.Title.Equals(template.Title, StringComparison.Ordinal))
            {
                quest.Title = template.Title;
                changed = true;
            }
        }

        if (changed)
        {
            Configuration.Save();
        }
    }

    private void RemovePreviousDailyQuests()
    {
        if (Configuration.CurrentDailyQuestIds.Count == 0)
        {
            return;
        }

        Configuration.SavedQuests.RemoveAll(q => Configuration.CurrentDailyQuestIds.Contains(q.Id));
        Configuration.CurrentDailyQuestIds.Clear();
    }
}

public sealed class TitleTier
{
    public TitleTier(string title, int requiredCompletions)
    {
        Title = title;
        RequiredCompletions = requiredCompletions;
    }

    public string Title { get; }
    public int RequiredCompletions { get; }
}

public sealed class SecretTitleTier
{
    public SecretTitleTier(string title, string emoteKey, int requiredCount)
    {
        Title = title;
        EmoteKey = emoteKey;
        RequiredCount = requiredCount;
    }

    public string Title { get; }
    public string EmoteKey { get; }
    public int RequiredCount { get; }
}

public class QuestProgress
{
    public bool IsComplete { get; set; }
    public int Progress { get; set; }
}

public class QuestFile
{
    public List<QuestData> Quests { get; set; } = new();
}

public class DailySocialQuestTemplate
{
    public DailySocialQuestTemplate(string title, string description, int goalCount, IEnumerable<string> triggerPhrases, IEnumerable<string> presets)
    {
        Title = title;
        Description = description;
        GoalCount = goalCount;
        TriggerPhrases = triggerPhrases.ToArray();
        Presets = presets.ToArray();
    }

    public string Title { get; }
    public string Description { get; }
    public int GoalCount { get; }
    public string[] TriggerPhrases { get; }
    public string[] Presets { get; }
}

public class QuestPackFile
{
    public string Name { get; set; } = "SocialMorpho Quest Pack";
    public string ExportedAt { get; set; } = string.Empty;
    public List<QuestData> Quests { get; set; } = new();
}
