using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;

namespace SocialMorpho.Services;

public sealed class NameplateTitleService : IDisposable
{
    private readonly Plugin plugin;
    private readonly INamePlateGui namePlateGui;
    private readonly IObjectTable objectTable;
    private readonly TitleSyncService titleSyncService;

    public NameplateTitleService(Plugin plugin, INamePlateGui namePlateGui, IObjectTable objectTable, TitleSyncService titleSyncService)
    {
        this.plugin = plugin;
        this.namePlateGui = namePlateGui;
        this.objectTable = objectTable;
        this.titleSyncService = titleSyncService;
        this.namePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!this.plugin.Configuration.ShowRewardTitleOnNameplate)
        {
            return;
        }

        var localId = this.objectTable.LocalPlayer?.GameObjectId ?? 0;
        if (localId == 0)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            var title = string.Empty;
            var colorPreset = "Gold";

            if (handler.GameObjectId == localId)
            {
                title = this.plugin.Configuration.Stats.UnlockedTitle;
                colorPreset = this.plugin.Configuration.RewardTitleColorPreset;
            }
            else if (this.plugin.Configuration.EnableTitleSync &&
                     this.plugin.Configuration.ShowSyncedTitles &&
                     this.titleSyncService.TryGetSyncedForGameObjectId(handler.GameObjectId, out var remote) &&
                     !string.IsNullOrWhiteSpace(remote.title))
            {
                title = remote.title;
                colorPreset = string.IsNullOrWhiteSpace(remote.colorPreset) ? "Gold" : remote.colorPreset;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var prefixedTitle = $"[{title}]";
            var (textColor, edgeColor) = GetColors(colorPreset);

            try
            {
                handler.DisplayTitle = true;
                handler.IsPrefixTitle = true;
                handler.SetField(NamePlateStringField.Title, prefixedTitle);
                handler.TextColor = textColor;
                handler.EdgeColor = edgeColor;
            }
            catch (Exception ex)
            {
                this.plugin.PluginLog.Warning($"Nameplate title update failed: {ex.Message}");
            }
        }
    }

    public void RequestRedraw()
    {
        this.namePlateGui.RequestRedraw();
    }

    private static (uint textColor, uint edgeColor) GetColors(string preset)
    {
        // FFXIV-style readable edge with accent text.
        var edge = 0xFF101010u;
        return preset switch
        {
            // NamePlate API expects ABGR ordering in the packed uint.
            "Pink" => (0xFFBB8BF0u, edge),
            "Cyan" => (0xFFFFD678u, edge),
            "Rose" => (0xFF9A9AF6u, edge),
            "Mint" => (0xFFC8F2A0u, edge),
            "Violet" => (0xFFE8A98Du, edge),
            "Gold Glow" => (0xFF83C3E5u, 0xFF3C2F15u),
            "Pink Glow" => (0xFFBB8BF0u, 0xFF3A1C39u),
            "Cyan Glow" => (0xFFFFD678u, 0xFF1A3A3Au),
            "Rose Glow" => (0xFF9A9AF6u, 0xFF3A203Au),
            "Mint Glow" => (0xFFC8F2A0u, 0xFF1E3824u),
            "Violet Glow" => (0xFFE8A98Du, 0xFF2A2140u),
            _ => (0xFF83C3E5u, edge), // Gold
        };
    }

    public void Dispose()
    {
        this.namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}
