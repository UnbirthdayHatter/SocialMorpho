using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;

namespace SocialMorpho.Services;

public sealed class NameplateTitleService : IDisposable
{
    private readonly Plugin plugin;
    private readonly INamePlateGui namePlateGui;
    private readonly IObjectTable objectTable;

    public NameplateTitleService(Plugin plugin, INamePlateGui namePlateGui, IObjectTable objectTable)
    {
        this.plugin = plugin;
        this.namePlateGui = namePlateGui;
        this.objectTable = objectTable;
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

        var title = this.plugin.Configuration.Stats.UnlockedTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var prefixedTitle = $"[{title}]";
        var (textColor, edgeColor) = GetColors(this.plugin.Configuration.RewardTitleColorPreset);

        foreach (var handler in handlers)
        {
            if (handler.GameObjectId != localId)
            {
                continue;
            }

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
            _ => (0xFF83C3E5u, edge), // Gold
        };
    }

    public void Dispose()
    {
        this.namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}
