using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using System.Reflection;

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
        if (this.plugin.IsHonorificBridgeActive())
        {
            // Honorific/Lightless handles rendering and colors in this mode.
            return;
        }

        var localEnabled = this.plugin.Configuration.ShowRewardTitleOnNameplate;
        var syncEnabled = this.plugin.Configuration.EnableTitleSync && this.plugin.Configuration.ShowSyncedTitles;
        if (!localEnabled && !syncEnabled)
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

            if (handler.GameObjectId == localId && localEnabled)
            {
                title = this.plugin.Configuration.Stats.UnlockedTitle;
                colorPreset = this.plugin.Configuration.RewardTitleColorPreset;
            }
            else if (syncEnabled &&
                     this.titleSyncService.TryGetSyncedForGameObjectId(handler.GameObjectId, out var remote) &&
                     !string.IsNullOrWhiteSpace(remote.title))
            {
                title = remote.title;
                colorPreset = string.IsNullOrWhiteSpace(remote.colorPreset) ? "Gold" : remote.colorPreset;
            }
            else if (syncEnabled &&
                     TryGetHandlerCharacterName(handler, out var handlerName) &&
                     this.titleSyncService.TryGetSyncedForCharacter(handlerName, out var byName) &&
                     !string.IsNullOrWhiteSpace(byName.title))
            {
                title = byName.title;
                colorPreset = string.IsNullOrWhiteSpace(byName.colorPreset) ? "Gold" : byName.colorPreset;
            }
            else if (syncEnabled &&
                     this.titleSyncService.TryGetCharacterNameForGameObjectId(handler.GameObjectId, out var objectTableName) &&
                     this.titleSyncService.TryGetSyncedForCharacter(objectTableName, out var byObjectName) &&
                     !string.IsNullOrWhiteSpace(byObjectName.title))
            {
                title = byObjectName.title;
                colorPreset = string.IsNullOrWhiteSpace(byObjectName.colorPreset) ? "Gold" : byObjectName.colorPreset;
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
                TrySetTitleOnlyColors(handler, textColor, edgeColor);
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

    private static bool TryGetHandlerCharacterName(INamePlateUpdateHandler handler, out string name)
    {
        name = string.Empty;
        var candidates = new[] { "Name", "PlayerName", "DisplayName", "ObjectName" };
        foreach (var candidate in candidates)
        {
            try
            {
                var prop = handler.GetType().GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance);
                var value = prop?.GetValue(handler);
                if (value == null)
                {
                    continue;
                }

                var textValueProp = value.GetType().GetProperty("TextValue", BindingFlags.Public | BindingFlags.Instance);
                var text = textValueProp?.GetValue(value) as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    name = text.Trim();
                    return true;
                }

                var raw = value.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    name = raw.Trim();
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static void TrySetTitleOnlyColors(INamePlateUpdateHandler handler, uint textColor, uint edgeColor)
    {
        // Avoid touching handler.TextColor/EdgeColor (that can tint player name text).
        // Use title-specific fields only when available in current Dalamud build.
        TrySetProperty(handler, "TitleTextColor", textColor);
        TrySetProperty(handler, "TitleEdgeColor", edgeColor);
        TrySetProperty(handler, "PrefixTextColor", textColor);
        TrySetProperty(handler, "PrefixEdgeColor", edgeColor);
    }

    private static void TrySetProperty(object target, string propertyName, uint value)
    {
        try
        {
            var p = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite && p.PropertyType == typeof(uint))
            {
                p.SetValue(target, value);
            }
        }
        catch
        {
        }
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
            "White Glow" => (0xFFF2F2F2u, 0xFF2A2A2Au),
            _ => (0xFF83C3E5u, edge), // Gold
        };
    }

    public void Dispose()
    {
        this.namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}
