using SocialMorpho.Data;

namespace SocialMorpho.Services;

public sealed class QuestBoardService
{
    private readonly Plugin plugin;
    private readonly QuestManager questManager;
    private const string ValentioneSeasonId = "valentione";

    private static readonly List<QuestBoardOffer> OfferPool = new()
    {
        new QuestBoardOffer { OfferId = "board_dote_duet", Title = "Petal Exchange", Description = "Have 4 different players use /dote on you", QuestType = QuestType.Social, GoalCount = 4, TriggerPhrases = new List<string> { "dotes on you", "dotes you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Common" },
        new QuestBoardOffer { OfferId = "board_hug_ring", Title = "Warm Circle", Description = "Have 4 different players use /hug on you", QuestType = QuestType.Social, GoalCount = 4, TriggerPhrases = new List<string> { "hugs you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Common" },
        new QuestBoardOffer { OfferId = "board_wave_train", Title = "Passing Greetings", Description = "Have 5 different players use /wave to you", QuestType = QuestType.Social, GoalCount = 5, TriggerPhrases = new List<string> { "waves to you" }, RiskClass = AntiCheeseRisk.Low, RarityLabel = "Common" },
        new QuestBoardOffer { OfferId = "board_kiss_chain", Title = "Rose Letters", Description = "Have 4 different players use /blowkiss on you", QuestType = QuestType.Social, GoalCount = 4, TriggerPhrases = new List<string> { "blows you a kiss", "blow kisses you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Uncommon" },
        new QuestBoardOffer { OfferId = "board_cheer_squad", Title = "Spotlight Hour", Description = "Have 5 different players use /cheer for you", QuestType = QuestType.Social, GoalCount = 5, TriggerPhrases = new List<string> { "cheers you on", "cheers for you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Uncommon" },
        new QuestBoardOffer { OfferId = "board_party_helper", Title = "Kindred Queue", Description = "Help a first-timer clear a duty 2 times", QuestType = QuestType.Custom, GoalCount = 2, TriggerPhrases = new List<string> { "one or more party members completed this duty for the first time" }, RiskClass = AntiCheeseRisk.High, RarityLabel = "Rare" },
        new QuestBoardOffer { OfferId = "board_comm_streak", Title = "Public Praise", Description = "Receive 3 player commendations", QuestType = QuestType.Custom, GoalCount = 3, TriggerPhrases = new List<string> { "commendation", "player commendation" }, RiskClass = AntiCheeseRisk.High, RarityLabel = "Rare" },
        new QuestBoardOffer { OfferId = "board_party_streak", Title = "Open Invitation", Description = "Join parties 3 times", QuestType = QuestType.Custom, GoalCount = 3, TriggerPhrases = new List<string> { "joined the party", "joins the party", "you have joined the party" }, RiskClass = AntiCheeseRisk.High, RarityLabel = "Rare" },
        new QuestBoardOffer { OfferId = "board_housing_tour", Title = "Doorstep Stories", Description = "Enter housing areas 2 times", QuestType = QuestType.Custom, GoalCount = 2, TriggerPhrases = new List<string> { "entered housing" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Uncommon" },
    };
    private static readonly List<QuestBoardOffer> SeasonalOfferPool = new()
    {
        new QuestBoardOffer { OfferId = "season_val_rose_mail", Title = "Scarlet Correspondence", Description = "Have 4 different players use /blowkiss on you", QuestType = QuestType.Social, GoalCount = 4, TriggerPhrases = new List<string> { "blows you a kiss", "blow kisses you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Seasonal", SeasonId = ValentioneSeasonId },
        new QuestBoardOffer { OfferId = "season_val_heart_halo", Title = "Heart Halo", Description = "Have 5 different players use /cheer for you", QuestType = QuestType.Social, GoalCount = 5, TriggerPhrases = new List<string> { "cheers you on", "cheers for you" }, RiskClass = AntiCheeseRisk.Medium, RarityLabel = "Seasonal", SeasonId = ValentioneSeasonId },
        new QuestBoardOffer { OfferId = "season_val_graceful_gift", Title = "Ribbon Courtesy", Description = "Have 4 different players use /bow to you", QuestType = QuestType.Social, GoalCount = 4, TriggerPhrases = new List<string> { "bows to you" }, RiskClass = AntiCheeseRisk.Low, RarityLabel = "Seasonal", SeasonId = ValentioneSeasonId },
    };

    public QuestBoardService(Plugin plugin, QuestManager questManager)
    {
        this.plugin = plugin;
        this.questManager = questManager;
    }

    public void Tick()
    {
        EnsureDailyBoardOffers(DateTime.Now);
    }

    public IReadOnlyList<QuestBoardOffer> GetAvailableOffers()
    {
        EnsureDailyBoardOffers(DateTime.Now);
        var active = ResolveActiveOffers();
        return active
            .Where(o => !this.plugin.Configuration.AcceptedQuestBoardOfferIds.Contains(o.OfferId, StringComparer.OrdinalIgnoreCase))
            .Where(o => !this.plugin.Configuration.DismissedQuestBoardOfferIds.Contains(o.OfferId, StringComparer.OrdinalIgnoreCase))
            .Where(o => !HasQuestForOffer(o.OfferId, DateTime.Now))
            .ToList();
    }

    public IReadOnlyList<QuestBoardOffer> GetAvailableSeasonalOffers()
    {
        var now = DateTime.Now;
        var season = GetActiveSeason(now);
        if (season == null)
        {
            return Array.Empty<QuestBoardOffer>();
        }

        return SeasonalOfferPool
            .Where(o => string.Equals(o.SeasonId, season.Value.SeasonId, StringComparison.OrdinalIgnoreCase))
            .Where(o => !this.plugin.Configuration.AcceptedQuestBoardOfferIds.Contains(o.OfferId, StringComparer.OrdinalIgnoreCase))
            .Where(o => !this.plugin.Configuration.DismissedQuestBoardOfferIds.Contains(o.OfferId, StringComparer.OrdinalIgnoreCase))
            .Where(o => !HasQuestForOffer(o.OfferId, now))
            .ToList();
    }

    public string GetActiveSeasonLabel()
    {
        var season = GetActiveSeason(DateTime.Now);
        return season == null ? string.Empty : season.Value.Label;
    }

    public string GetActiveSeasonCountdownLabel()
    {
        var now = DateTime.Now;
        var season = GetActiveSeason(now);
        if (season == null)
        {
            return string.Empty;
        }

        var remaining = season.Value.EndLocal - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "Ends soon";
        }

        return $"{remaining.Days}d {remaining.Hours}h remaining";
    }

    public string GetUpcomingSeasonPreviewLabel()
    {
        var now = DateTime.Now;
        var next = GetUpcomingSeason(now);
        if (next == null)
        {
            return string.Empty;
        }

        var until = next.Value.StartLocal - now;
        if (until < TimeSpan.Zero)
        {
            until = TimeSpan.Zero;
        }

        return $"{next.Value.Label} starts in {until.Days}d {until.Hours}h";
    }

    public bool AcceptOffer(string offerId)
    {
        EnsureDailyBoardOffers(DateTime.Now);
        var offer = ResolveActiveOffers()
            .Concat(GetAvailableSeasonalOffers())
            .FirstOrDefault(o => o.OfferId.Equals(offerId, StringComparison.OrdinalIgnoreCase));
        if (offer == null)
        {
            return false;
        }

        var questId = BuildQuestIdForOffer(offer.OfferId, DateTime.Now);
        if (this.questManager.GetQuest(questId) != null)
        {
            return false;
        }

        this.questManager.AddQuest(new QuestData
        {
            Id = questId,
            Title = offer.Title,
            Description = offer.Description,
            Type = offer.QuestType,
            GoalCount = offer.GoalCount,
            CurrentCount = 0,
            Completed = false,
            CreatedAt = DateTime.Now,
            ResetSchedule = ResetSchedule.Daily,
            LastResetDate = DateTime.Now,
            TriggerPhrases = offer.TriggerPhrases.ToList(),
            AntiCheeseRisk = offer.RiskClass,
            SeasonId = offer.SeasonId ?? string.Empty,
        });

        if (!this.plugin.Configuration.AcceptedQuestBoardOfferIds.Contains(offer.OfferId, StringComparer.OrdinalIgnoreCase))
        {
            this.plugin.Configuration.AcceptedQuestBoardOfferIds.Add(offer.OfferId);
        }

        this.plugin.Configuration.DismissedQuestBoardOfferIds.RemoveAll(x => x.Equals(offer.OfferId, StringComparison.OrdinalIgnoreCase));
        this.plugin.Configuration.Save();
        return true;
    }

    public void DismissOffer(string offerId)
    {
        EnsureDailyBoardOffers(DateTime.Now);
        if (!this.plugin.Configuration.DismissedQuestBoardOfferIds.Contains(offerId, StringComparer.OrdinalIgnoreCase))
        {
            this.plugin.Configuration.DismissedQuestBoardOfferIds.Add(offerId);
            this.plugin.Configuration.Save();
        }
    }

    private void EnsureDailyBoardOffers(DateTime now)
    {
        if (this.plugin.Configuration.LastQuestBoardRefreshDate?.Date == now.Date &&
            this.plugin.Configuration.ActiveQuestBoardOfferIds.Count >= 3)
        {
            return;
        }

        var seed = HashCode.Combine(now.Year, now.DayOfYear, 0x5F3759DF);
        var rng = new Random(seed);
        var picked = OfferPool.OrderBy(_ => rng.Next()).Take(5).Select(o => o.OfferId).ToList();

        this.plugin.Configuration.ActiveQuestBoardOfferIds = picked;
        this.plugin.Configuration.AcceptedQuestBoardOfferIds.Clear();
        this.plugin.Configuration.DismissedQuestBoardOfferIds.Clear();
        this.plugin.Configuration.LastQuestBoardRefreshDate = now.Date;
        this.plugin.Configuration.Save();
    }

    private List<QuestBoardOffer> ResolveActiveOffers()
    {
        var set = this.plugin.Configuration.ActiveQuestBoardOfferIds;
        if (set.Count == 0)
        {
            return OfferPool.Take(5).ToList();
        }

        return set
            .Select(id => OfferPool.FirstOrDefault(o => o.OfferId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(o => o != null)
            .Cast<QuestBoardOffer>()
            .ToList();
    }

    private bool HasQuestForOffer(string offerId, DateTime now)
    {
        var questId = BuildQuestIdForOffer(offerId, now);
        return this.questManager.GetQuest(questId) != null;
    }

    private static ulong BuildQuestIdForOffer(string offerId, DateTime now)
    {
        var hash = Math.Abs(HashCode.Combine(offerId.ToLowerInvariant(), now.Year, now.DayOfYear));
        return 980000UL + (ulong)(hash % 10000);
    }

    private static (string SeasonId, string Label, DateTime StartLocal, DateTime EndLocal)? GetActiveSeason(DateTime now)
    {
        // Valentione window: Feb 1 - Mar 1 (inclusive start, exclusive end).
        var start = new DateTime(now.Year, 2, 1, 0, 0, 0, DateTimeKind.Local);
        var end = new DateTime(now.Year, 3, 1, 0, 0, 0, DateTimeKind.Local);
        if (now >= start && now < end)
        {
            return (ValentioneSeasonId, "Valentione Festival", start, end);
        }

        return null;
    }

    private static (string SeasonId, string Label, DateTime StartLocal, DateTime EndLocal)? GetUpcomingSeason(DateTime now)
    {
        // For now we preview the next Valentione window.
        var start = new DateTime(now.Year, 2, 1, 0, 0, 0, DateTimeKind.Local);
        if (now >= start)
        {
            start = start.AddYears(1);
        }

        var end = start.AddMonths(1);
        return (ValentioneSeasonId, "Valentione Festival", start, end);
    }
}
