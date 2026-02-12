using Dalamud.Plugin.Services;
using SocialMorpho.Data;
using SocialMorpho.Windows;

namespace SocialMorpho.Services;

public class QuestOfferService : IDisposable
{
    private const int CurrentCatalogVersion = 1;

    private readonly Plugin plugin;
    private readonly IClientState clientState;
    private readonly IPluginLog pluginLog;
    private readonly QuestOfferWindow questOfferWindow;

    private static readonly List<QuestOfferDefinition> QuestOffers = new()
    {
        new QuestOfferDefinition
        {
            OfferId = "offer_secret_lily",
            CatalogVersion = 1,
            PopupTitle = "Secret of the Lily",
            PopupDescription = "A soft breeze carries a familiar smile. Spend time with your companions and let warm gestures bloom into something unforgettable.",
            ImageFileName = "Square.png",
            QuestId = 920001,
            QuestTitle = "Petals in the Wind",
            QuestDescription = "Have 3 different players use /blowkiss on you",
            QuestType = QuestType.Social,
            GoalCount = 3,
            TriggerPhrases = new List<string> { "blows you a kiss", "blow kisses you" },
        },
    };

    public QuestOfferService(
        Plugin plugin,
        IClientState clientState,
        IPluginLog pluginLog,
        QuestOfferWindow questOfferWindow)
    {
        this.plugin = plugin;
        this.clientState = clientState;
        this.pluginLog = pluginLog;
        this.questOfferWindow = questOfferWindow;

        this.clientState.Login += this.OnLogin;

        if (this.clientState.IsLoggedIn)
        {
            this.TryShowOfferForToday();
        }
    }

    private void OnLogin()
    {
        this.TryShowOfferForToday();
    }

    private void TryShowOfferForToday()
    {
        try
        {
            var today = DateTime.Today;
            if (this.plugin.Configuration.LastQuestOfferPopupDate?.Date == today)
            {
                return;
            }

            var offer = this.GetPendingOfferForCurrentCatalog();
            if (offer == null)
            {
                return;
            }

            this.plugin.Configuration.LastQuestOfferPopupDate = today;
            this.plugin.Configuration.Save();

            this.questOfferWindow.ShowOffer(offer, this.AcceptOffer, this.DeclineOffer);
            this.pluginLog.Info($"Showing quest offer popup: {offer.OfferId}");
        }
        catch (Exception ex)
        {
            this.pluginLog.Error($"Failed showing quest offer popup: {ex.Message}");
        }
    }

    public bool TriggerTestOfferPopup()
    {
        try
        {
            var offer = this.GetPendingOfferForCurrentCatalog() ?? QuestOffers.FirstOrDefault();
            if (offer == null)
            {
                return false;
            }

            this.questOfferWindow.ShowOffer(offer, this.AcceptOffer, this.DeclineOffer);
            this.pluginLog.Info($"Showing quest offer popup (manual test): {offer.OfferId}");
            return true;
        }
        catch (Exception ex)
        {
            this.pluginLog.Error($"Failed showing manual quest offer popup: {ex.Message}");
            return false;
        }
    }

    private QuestOfferDefinition? GetPendingOfferForCurrentCatalog()
    {
        return QuestOffers.FirstOrDefault(o =>
            o.CatalogVersion == CurrentCatalogVersion &&
            !this.plugin.Configuration.ProcessedQuestOfferIds.Contains(o.OfferId, StringComparer.OrdinalIgnoreCase));
    }

    private void AcceptOffer(QuestOfferDefinition offer)
    {
        var quest = new QuestData
        {
            Id = offer.QuestId,
            Title = offer.QuestTitle,
            Description = offer.QuestDescription,
            Type = offer.QuestType,
            GoalCount = offer.GoalCount,
            CurrentCount = 0,
            Completed = false,
            TriggerPhrases = offer.TriggerPhrases.ToList(),
        };

        this.plugin.QuestManager.AddQuest(quest);
        this.MarkOfferProcessed(offer.OfferId);
        this.plugin.PluginLog.Info($"Accepted quest offer: {offer.OfferId}");
    }

    private void DeclineOffer(QuestOfferDefinition offer)
    {
        this.plugin.QuestManager.RemoveQuest(offer.QuestId);
        this.MarkOfferProcessed(offer.OfferId);
        this.plugin.PluginLog.Info($"Declined quest offer: {offer.OfferId}");
    }

    private void MarkOfferProcessed(string offerId)
    {
        if (!this.plugin.Configuration.ProcessedQuestOfferIds.Contains(offerId, StringComparer.OrdinalIgnoreCase))
        {
            this.plugin.Configuration.ProcessedQuestOfferIds.Add(offerId);
        }

        this.plugin.Configuration.Save();
    }

    public void Dispose()
    {
        this.clientState.Login -= this.OnLogin;
    }
}
