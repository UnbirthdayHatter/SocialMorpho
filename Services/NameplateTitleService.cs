using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using System.Reflection;
using System.Text.RegularExpressions;

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
            else if (syncEnabled &&
                     this.titleSyncService.TryGetHonorificTitleForGameObjectId(handler.GameObjectId, out var honorificTitle) &&
                     !string.IsNullOrWhiteSpace(honorificTitle.title))
            {
                title = honorificTitle.title;
                colorPreset = string.IsNullOrWhiteSpace(honorificTitle.colorPreset) ? "Gold" : honorificTitle.colorPreset;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            title = NormalizeDisplayTitle(title);
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
                TrySetTitleOnlyColors(handler, textColor, edgeColor, this.plugin.Configuration.ForceSocialMorphoTitleColors);
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

    private static void TrySetTitleOnlyColors(INamePlateUpdateHandler handler, uint textColor, uint edgeColor, bool forceOverride)
    {
        // Avoid touching handler.TextColor/EdgeColor (that can tint player name text).
        // Use title-specific fields only when available in current Dalamud build.
        TrySetProperty(handler, "TitleTextColor", textColor);
        TrySetProperty(handler, "TitleEdgeColor", edgeColor);
        TrySetProperty(handler, "PrefixTextColor", textColor);
        TrySetProperty(handler, "PrefixEdgeColor", edgeColor);

        if (!forceOverride)
        {
            return;
        }

        // Some plugins/styles paint through alternate fields; force-write common fallbacks.
        TrySetProperty(handler, "Color", textColor);
        TrySetProperty(handler, "EdgeColor", edgeColor);
        TrySetProperty(handler, "TextColor", textColor);
        TrySetProperty(handler, "NameTextColor", textColor);
        TrySetProperty(handler, "NameEdgeColor", edgeColor);
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
        // Nameplate packed colors use ABGR ordering.
        var edge = ToAbgr(0x10, 0x10, 0x10);
        var gradientPulse = (MathF.Sin((float)(DateTime.UtcNow.TimeOfDay.TotalSeconds * 2.25f)) + 1f) * 0.5f;
        return preset switch
        {
            "Pink" => (ToAbgr(0xF0, 0x8B, 0xBB), edge),
            "Cyan" => (ToAbgr(0x78, 0xD6, 0xFF), edge),
            "Rose" => (ToAbgr(0xF6, 0x9A, 0x9A), edge),
            "Mint" => (ToAbgr(0xA0, 0xF2, 0xC8), edge),
            "Violet" => (ToAbgr(0x8D, 0xA9, 0xE8), edge),
            "Gold Gradient" => (BlendToAbgr(0xE5, 0xC3, 0x83, 0xFF, 0xEE, 0xB8, gradientPulse), ToAbgr(0x2E, 0x24, 0x12)),
            "Rose Gradient" => (BlendToAbgr(0xF6, 0x9A, 0x9A, 0xFF, 0xC4, 0xDA, gradientPulse), ToAbgr(0x3A, 0x20, 0x2D)),
            "Cyan Gradient" => (BlendToAbgr(0x78, 0xD6, 0xFF, 0xB8, 0xF2, 0xFF, gradientPulse), ToAbgr(0x1B, 0x33, 0x3B)),
            "Violet Gradient" => (BlendToAbgr(0x8D, 0xA9, 0xE8, 0xC0, 0xB4, 0xFF, gradientPulse), ToAbgr(0x2A, 0x21, 0x40)),
            "Rainbow Gradient" => (RainbowToAbgr(gradientPulse), ToAbgr(0x24, 0x24, 0x24)),
            "Gold Glow" => (ToAbgr(0xE5, 0xC3, 0x83), ToAbgr(0x3C, 0x2F, 0x15)),
            "Pink Glow" => (ToAbgr(0xF0, 0x8B, 0xBB), ToAbgr(0x3A, 0x1C, 0x39)),
            "Cyan Glow" => (ToAbgr(0x78, 0xD6, 0xFF), ToAbgr(0x1A, 0x3A, 0x3A)),
            "Rose Glow" => (ToAbgr(0xF6, 0x9A, 0x9A), ToAbgr(0x3A, 0x20, 0x3A)),
            "Mint Glow" => (ToAbgr(0xA0, 0xF2, 0xC8), ToAbgr(0x1E, 0x38, 0x24)),
            "Violet Glow" => (ToAbgr(0x8D, 0xA9, 0xE8), ToAbgr(0x2A, 0x21, 0x40)),
            "White Glow" => (ToAbgr(0xF2, 0xF2, 0xF2), ToAbgr(0x2A, 0x2A, 0x2A)),
            _ => (ToAbgr(0xE5, 0xC3, 0x83), edge), // Gold
        };
    }

    private static uint ToAbgr(byte r, byte g, byte b)
    {
        return 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
    }

    private static uint BlendToAbgr(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        var r = (byte)MathF.Round((r1 * (1f - clamped)) + (r2 * clamped));
        var g = (byte)MathF.Round((g1 * (1f - clamped)) + (g2 * clamped));
        var b = (byte)MathF.Round((b1 * (1f - clamped)) + (b2 * clamped));
        return ToAbgr(r, g, b);
    }

    private static uint RainbowToAbgr(float t)
    {
        var h = Math.Clamp(t, 0f, 1f) * 6f;
        var i = (int)MathF.Floor(h);
        var f = h - i;
        const float v = 1f;
        const float s = 0.55f;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var u = v * (1f - (1f - f) * s);

        (float r, float g, float b) = (i % 6) switch
        {
            0 => (v, u, p),
            1 => (q, v, p),
            2 => (p, v, u),
            3 => (p, q, v),
            4 => (u, p, v),
            _ => (v, p, q),
        };

        return ToAbgr((byte)MathF.Round(r * 255f), (byte)MathF.Round(g * 255f), (byte)MathF.Round(b * 255f));
    }

    private static string? NormalizeDisplayTitle(string rawTitle)
    {
        var trimmed = rawTitle.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        // Guard against Honorific payload blobs accidentally being treated as title text.
        var looksLikePayload =
            (trimmed.Contains("\"Title\"", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains("\\\"Title\\\"", StringComparison.OrdinalIgnoreCase)) &&
            trimmed.Contains("GradientAnimationStyle", StringComparison.OrdinalIgnoreCase);
        if (!looksLikePayload)
        {
            return StripWrappingQuotes(trimmed);
        }

        var m = Regex.Match(trimmed, "\\\"Title\\\"\\s*:\\s*\\\"(?<t>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.CultureInvariant);
        if (!m.Success)
        {
            return null;
        }

        var decoded = m.Groups["t"].Value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrWhiteSpace(decoded) ? null : StripWrappingQuotes(decoded);
    }

    private static string StripWrappingQuotes(string value)
    {
        var result = value;
        if (result.Length >= 2)
        {
            if ((result[0] == '"' && result[^1] == '"') ||
                (result[0] == '\'' && result[^1] == '\''))
            {
                result = result[1..^1].Trim();
            }
        }

        return result;
    }

    public void Dispose()
    {
        this.namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}
