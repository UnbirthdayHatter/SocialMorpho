using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace SocialMorpho;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool SoundEnabled { get; set; } = true;
    public float PanelOpacity { get; set; } = 0.95f;
    public bool CompactMode { get; set; } = false;

    // Quest data
    public List<QuestData> SavedQuests { get; set; } = new();

    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }

    public void Save()
    {
        PluginInterface?.SavePluginConfig(this);
    }
}