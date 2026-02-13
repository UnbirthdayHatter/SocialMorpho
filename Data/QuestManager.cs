using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SocialMorpho.Data;

public class QuestManager
{
    private const ulong DailyQuestIdBase = 900000;
    private readonly Configuration Configuration;
    private readonly Dictionary<ulong, QuestProgress> QuestProgress = new();
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
    };

    public QuestManager(Configuration configuration)
    {
        Configuration = configuration;
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
            quest.Completed = true;
            quest.CompletedAt = DateTime.Now;
            RegisterCompletion(DateTime.Now);
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
        return Configuration.SavedQuests.Where(q => !q.Completed).ToList();
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
        var quest = Configuration.SavedQuests.FirstOrDefault(q =>
            !q.Completed &&
            q.TriggerPhrases.Any(p => normalized.Contains(p, StringComparison.OrdinalIgnoreCase)));

        if (quest == null)
        {
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

    public void EnsureDailySocialQuests(DateTime now)
    {
        RefreshDailyQuestTitles();

        if (Configuration.LastDailyQuestSelectionDate?.Date == now.Date &&
            Configuration.CurrentDailyQuestIds.Count == 3 &&
            Configuration.CurrentDailyQuestIds.All(id => Configuration.SavedQuests.Any(q => q.Id == id)))
        {
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

    private void RegisterCompletion(DateTime now)
    {
        EnsureStatsBuckets(now);

        var stats = Configuration.Stats;
        stats.TotalCompletions++;
        stats.WeeklyCompletions++;
        stats.WeeklyRank = GetWeeklyRank(stats.WeeklyCompletions);
        stats.UnlockedTitle = GetUnlockedTitle(stats.TotalCompletions);

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

    private static string GetUnlockedTitle(int totalCompletions)
    {
        if (totalCompletions >= 75) return "Heart of Eorzea";
        if (totalCompletions >= 30) return "Social Star";
        if (totalCompletions >= 10) return "Budding Friend";
        return "New Adventurer";
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
