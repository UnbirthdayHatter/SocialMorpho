using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialMorpho.Data;

public class QuestManager
{
    private readonly Configuration _config;
    public event Action? OnQuestsChanged;

    public QuestManager(Configuration config)
    {
        _config = config;
        LoadDefaultQuestsIfEmpty();
    }

    private void LoadDefaultQuestsIfEmpty()
    {
        if (_config.SavedQuests.Count == 0)
        {
            _config.SavedQuests.AddRange(new List<QuestData>
            {
                new()
                {
                    Id = (ulong)DateTime.Now.Ticks + 1,
                    Title = "Get Dotted Three Times",
                    Description = "Receive DoT effects from 3 different players",
                    Type = QuestType.Buff,
                    GoalCount = 3,
                    CreatedAt = DateTime.Now
                },
                new()
                {
                    Id = (ulong)DateTime.Now.Ticks + 2,
                    Title = "Hug Four Players",
                    Description = "Use the hug emote on 4 different party members",
                    Type = QuestType.Emote,
                    GoalCount = 4,
                    CreatedAt = DateTime.Now
                },
                new()
                {
                    Id = (ulong)DateTime.Now.Ticks + 3,
                    Title = "Social Butterfly",
                    Description = "Use 5 social actions with different players",
                    Type = QuestType.Social,
                    GoalCount = 5,
                    CreatedAt = DateTime.Now
                }
            });
            _config.Save();
        }
    }

    public void AddQuest(string title, string description, int goalCount, QuestType type = QuestType.Custom)
    {
        var quest = new QuestData
        {
            Id = (ulong)DateTime.Now.Ticks,
            Title = title,
            Description = description,
            Type = type,
            GoalCount = goalCount,
            CreatedAt = DateTime.Now
        };

        _config.SavedQuests.Add(quest);
        _config.Save();
        OnQuestsChanged?.Invoke();
        PluginLog.Information($"Quest added: {title}");
    }

    public void UpdateQuestProgress(ulong questId, int increment = 1)
    {
        var quest = _config.SavedQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null && !quest.Completed)
        {
            quest.CurrentCount = Math.Min(quest.CurrentCount + increment, quest.GoalCount);
            if (quest.CurrentCount >= quest.GoalCount)
            {
                quest.Completed = true;
                quest.CompletedAt = DateTime.Now;
                PluginLog.Information($"Quest completed: {quest.Title}");
            }
            _config.Save();
            OnQuestsChanged?.Invoke();
        }
    }

    public void ResetQuest(ulong questId)
    {
        var quest = _config.SavedQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            quest.CurrentCount = 0;
            quest.Completed = false;
            quest.CompletedAt = null;
            _config.Save();
            OnQuestsChanged?.Invoke();
            PluginLog.Information($"Quest reset: {quest.Title}");
        }
    }

    public void DeleteQuest(ulong questId)
    {
        var quest = _config.SavedQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            _config.SavedQuests.Remove(quest);
            _config.Save();
            OnQuestsChanged?.Invoke();
            PluginLog.Information($"Quest deleted: {quest.Title}");
        }
    }

    public List<QuestData> GetAllQuests() => _config.SavedQuests;
    public List<QuestData> GetActiveQuests() => _config.SavedQuests.Where(q => !q.Completed).ToList();
    public List<QuestData> GetCompletedQuests() => _config.SavedQuests.Where(q => q.Completed).ToList();
}