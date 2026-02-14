using System;
using System.Collections.Generic;

namespace SocialMorpho.Data;

[Serializable]
public class QuestData
{
    public ulong Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestType Type { get; set; } = QuestType.Custom;
    public int GoalCount { get; set; } = 1;
    public int CurrentCount { get; set; } = 0;
    public bool Completed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public ResetSchedule ResetSchedule { get; set; } = ResetSchedule.None;
    public DateTime? LastResetDate { get; set; }
    public List<string> TriggerPhrases { get; set; } = new();
    public AntiCheeseRisk AntiCheeseRisk { get; set; } = AntiCheeseRisk.Medium;
    public string SeasonId { get; set; } = string.Empty;
    public bool RequiresDuoPartner { get; set; } = false;
}

public enum QuestType
{
    Social,
    Buff,
    Emote,
    Custom
}

public enum ResetSchedule
{
    None,
    Daily,
    Weekly
}

public enum AntiCheeseRisk
{
    Low,
    Medium,
    High
}
