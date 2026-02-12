using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SocialMorpho.Data;

public class QuestManager
{
    private const ulong DailyQuestIdBase = 900000;
    private Configuration Configuration;
    private Dictionary<ulong, QuestProgress> QuestProgress = new();
    private static readonly List<DailySocialQuestTemplate> DailySocialQuestTemplates = new()
    {
        new DailySocialQuestTemplate("Sweet on You", "Have 3 different players use /dote on you", 3, new[] { "dotes on you", "dotes you" }),
        new DailySocialQuestTemplate("Spread the Love", "Have 3 different players use /hug on you", 3, new[] { "hugs you" }),
        new DailySocialQuestTemplate("Kiss Blown Your Way", "Have 3 different players use /blowkiss on you", 3, new[] { "blows you a kiss", "blow kisses you" }),
        new DailySocialQuestTemplate("Dance Fever", "Have 3 different players use /dance with you", 3, new[] { "dances with you", "dances for you" }),
        new DailySocialQuestTemplate("Friendly Faces", "Have 3 different players use /wave to you", 3, new[] { "waves to you" }),
        new DailySocialQuestTemplate("Hype Circle", "Have 3 different players use /cheer for you", 3, new[] { "cheers you on", "cheers for you" }),
        new DailySocialQuestTemplate("Bow Trio", "Have 3 different players use /bow to you", 3, new[] { "bows to you" }),
        new DailySocialQuestTemplate("Respect Given", "Have 3 different players use /salute to you", 3, new[] { "salutes you" }),
        new DailySocialQuestTemplate("Good Vibes Only", "Have 3 different players use /thumbsup to you", 3, new[] { "gives you a thumbs up", "gives you the thumbs up" }),
    };

    public QuestManager(Configuration configuration)
    {
        Configuration = configuration;
        LoadProgressFromConfig();
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
        if (quest != null)
        {
            quest.Completed = true;
            quest.CompletedAt = DateTime.Now;
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
        return IncrementQuestProgressFromChat("dotes on you");
    }

    public bool IncrementQuestProgressFromChat(string chatText)
    {
        if (string.IsNullOrWhiteSpace(chatText))
        {
            return false;
        }

        var normalized = chatText.Trim();
        var quest = Configuration.SavedQuests.FirstOrDefault(q =>
            !q.Completed &&
            q.TriggerPhrases.Any(p => normalized.Contains(p, StringComparison.OrdinalIgnoreCase)));

        if (quest == null)
        {
            return false;
        }

        quest.CurrentCount = Math.Min(quest.GoalCount, quest.CurrentCount + 1);
        if (quest.CurrentCount >= quest.GoalCount)
        {
            quest.Completed = true;
            quest.CompletedAt = DateTime.Now;
        }

        Configuration.Save();
        return true;
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

        var rng = new Random(now.Date.GetHashCode());
        var selectedTemplates = DailySocialQuestTemplates
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
    public DailySocialQuestTemplate(string title, string description, int goalCount, IEnumerable<string> triggerPhrases)
    {
        Title = title;
        Description = description;
        GoalCount = goalCount;
        TriggerPhrases = triggerPhrases.ToArray();
    }

    public string Title { get; }
    public string Description { get; }
    public int GoalCount { get; }
    public string[] TriggerPhrases { get; }
}
