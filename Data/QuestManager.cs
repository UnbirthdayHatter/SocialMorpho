using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SocialMorpho.Data;

public class QuestManager
{
    private Configuration Configuration;
    private Dictionary<ulong, QuestProgress> QuestProgress = new();

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