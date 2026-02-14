using Dalamud.Configuration;
using Dalamud.Plugin;
using SocialMorpho.Data;
using System;
using System.Collections.Generic;

namespace SocialMorpho;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const string DefaultTitleSyncApiUrl = "https://socialmorpho-sync-api.socialmorpho.workers.dev";

    public int Version { get; set; } = 1;

    public bool SoundEnabled { get; set; } = true;
    public float PanelOpacity { get; set; } = 0.95f;
    public bool CompactMode { get; set; } = false;

    // Quest data
    public List<QuestData> SavedQuests { get; set; } = new();
    public DateTime? LastDailyQuestSelectionDate { get; set; }
    public List<ulong> CurrentDailyQuestIds { get; set; } = new();
    public DateTime? LastQuestOfferPopupDate { get; set; }
    public List<string> ProcessedQuestOfferIds { get; set; } = new();
    public string ActiveQuestPreset { get; set; } = "Solo";
    public SocialStats Stats { get; set; } = new();
    public bool ShowRewardTitleOnNameplate { get; set; } = false;
    public string RewardTitleColorPreset { get; set; } = "Gold";
    public bool ForceSocialMorphoTitleColors { get; set; } = false;
    public string SelectedStarterTitle { get; set; } = "New Adventurer";
    public bool EnableTitleSync { get; set; } = true;
    public bool ShareTitleSync { get; set; } = true;
    public bool ShowSyncedTitles { get; set; } = true;
    public bool PreferHonorificSync { get; set; } = true;
    public string TitleSyncApiUrl { get; set; } = DefaultTitleSyncApiUrl;
    public string TitleSyncApiKey { get; set; } = string.Empty;

    // Quest Tracker settings
    public bool ShowQuestTracker { get; set; } = true;
    public bool ShowQuestTrackerOnLogin { get; set; } = true;
    public bool ShowLoginNotification { get; set; } = true;
    public bool AutoLoadJsonQuests { get; set; } = false;

    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }

    public void Save()
    {
        PluginInterface?.SavePluginConfig(this);
    }
}
