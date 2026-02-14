using System;
using System.Collections.Generic;

namespace SocialMorpho.Data;

[Serializable]
public class SocialStats
{
    public int TotalProgressTicks { get; set; }
    public int TotalCompletions { get; set; }
    public int CurrentStreakDays { get; set; }
    public int BestStreakDays { get; set; }
    public DateTime? LastCompletionDate { get; set; }
    public DateTime? WeeklyBucketStart { get; set; }
    public int WeeklyCompletions { get; set; }
    public string WeeklyRank { get; set; } = "Sproutling";
    public string UnlockedTitle { get; set; } = "New Adventurer";
    public List<DailyCompletionEntry> RecentDailyCompletions { get; set; } = new();
    public Dictionary<string, int> EmoteReceivedCounts { get; set; } = new();
}

[Serializable]
public class DailyCompletionEntry
{
    public string Date { get; set; } = string.Empty; // yyyy-MM-dd
    public int Count { get; set; }
}

public class ProgressUpdateResult
{
    public required ulong QuestId { get; init; }
    public required string QuestTitle { get; init; }
    public required QuestType QuestType { get; init; }
    public required int Delta { get; init; }
    public required int NewCount { get; init; }
    public required int GoalCount { get; init; }
    public required bool CompletedNow { get; init; }
    public string? ChainBonusQuestTitle { get; init; }
    public BonusQuestOfferPayload? BonusOffer { get; init; }
}

public class BonusQuestOfferPayload
{
    public required ulong QuestId { get; init; }
    public required string QuestTitle { get; init; }
    public required string QuestDescription { get; init; }
    public required QuestType QuestType { get; init; }
    public required int GoalCount { get; init; }
    public List<string> TriggerPhrases { get; init; } = new();
}

public class TitleProgressInfo
{
    public required string CurrentTitle { get; init; }
    public required string NextTitle { get; init; }
    public required int CurrentCompletions { get; init; }
    public required int NextRequirement { get; init; }
    public required int RemainingToNext { get; init; }
}

public class SecretTitleProgressInfo
{
    public required string Title { get; init; }
    public required string EmoteKey { get; init; }
    public required int CurrentCount { get; init; }
    public required int Requirement { get; init; }
    public required bool Unlocked { get; init; }
}
