using System.Collections.Generic;

namespace SocialMorpho.Data;

public sealed class QuestBoardOffer
{
    public required string OfferId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required QuestType QuestType { get; init; }
    public required int GoalCount { get; init; }
    public required List<string> TriggerPhrases { get; init; }
    public required AntiCheeseRisk RiskClass { get; init; }
    public required string RarityLabel { get; init; }
    public string SeasonId { get; init; } = string.Empty;
}
