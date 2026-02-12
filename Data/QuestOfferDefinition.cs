using System.Collections.Generic;

namespace SocialMorpho.Data;

public class QuestOfferDefinition
{
    public required string OfferId { get; init; }
    public required int CatalogVersion { get; init; }
    public required string PopupTitle { get; init; }
    public required string PopupDescription { get; init; }
    public required string ImageFileName { get; init; }

    public required ulong QuestId { get; init; }
    public required string QuestTitle { get; init; }
    public required string QuestDescription { get; init; }
    public required QuestType QuestType { get; init; }
    public required int GoalCount { get; init; }
    public List<string> TriggerPhrases { get; init; } = new();
}
