using System;
using System.Collections.Generic;
using System.Linq;

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

    public QuestProgress? GetQuestProgress(ulong questId)
    {
        return QuestProgress.TryGetValue(questId, out var progress) ? progress : null;
    }
}

public class QuestProgress
{
    public bool IsComplete { get; set; }
    public int Progress { get; set; }
}