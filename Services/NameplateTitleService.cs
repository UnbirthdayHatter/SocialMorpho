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
                colorPreset = ResolveLocalColorPreset(title);
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

    public bool ShouldAnimateGradientTitles()
    {
        if (!this.plugin.Configuration.ShowRewardTitleOnNameplate)
        {
            return false;
        }

        var localPreset = ResolveLocalColorPreset(this.plugin.Configuration.Stats.UnlockedTitle);
        if (IsGradientPreset(localPreset))
        {
            return true;
        }

        foreach (var synced in this.titleSyncService.GetSyncedTitleSnapshot(24))
        {
            if (IsGradientPreset(synced.colorPreset))
            {
                return true;
            }
        }

        return false;
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
            "Gold Gradient" => (BlendToAbgr(0xD2, 0xA8, 0x57, 0xFF, 0xF0, 0xBF, gradientPulse), ToAbgr(0x2E, 0x24, 0x12)),
            "Rose Gradient" => (BlendToAbgr(0xD8, 0x79, 0x86, 0xFF, 0xC7, 0xDE, gradientPulse), ToAbgr(0x3A, 0x20, 0x2D)),
            "Cyan Gradient" => (BlendToAbgr(0x4C, 0xB8, 0xE8, 0xC7, 0xF7, 0xFF, gradientPulse), ToAbgr(0x1B, 0x33, 0x3B)),
            "Violet Gradient" => (BlendToAbgr(0x6E, 0x86, 0xD8, 0xD1, 0xB9, 0xFF, gradientPulse), ToAbgr(0x2A, 0x21, 0x40)),
            "Sunset Gradient" => (BlendToAbgr(0xFF, 0xA2, 0x5F, 0xFF, 0x5E, 0x8A, gradientPulse), ToAbgr(0x3A, 0x1F, 0x24)),
            "Seafoam Gradient" => (BlendToAbgr(0x6C, 0xD9, 0xC0, 0x5D, 0xA9, 0xE8, gradientPulse), ToAbgr(0x1A, 0x30, 0x36)),
            "Valentione Gradient" => (BlendToAbgr(0xFF, 0xB4, 0xD0, 0xFF, 0x77, 0xAA, gradientPulse), ToAbgr(0x3A, 0x1F, 0x2D)),
            "Companion Gradient" => (BlendToAbgr(0xF8, 0xD7, 0xA8, 0xE6, 0xA5, 0x7A, gradientPulse), ToAbgr(0x3A, 0x2A, 0x1E)),
            "Confidant Gradient" => (BlendToAbgr(0xA2, 0xD8, 0xFF, 0x9A, 0xB1, 0xE8, gradientPulse), ToAbgr(0x1E, 0x2A, 0x3A)),
            "Muse Gradient" => (BlendToAbgr(0xD9, 0xB7, 0xFF, 0xFF, 0xB9, 0xD4, gradientPulse), ToAbgr(0x2C, 0x22, 0x34)),
            "Heartbound Gradient" => (BlendToAbgr(0xFF, 0xD0, 0x90, 0xFF, 0x7E, 0x9E, gradientPulse), ToAbgr(0x3A, 0x22, 0x26)),
            "Lily Gradient" => (BlendToAbgr(0xF7, 0xE3, 0xFF, 0xE8, 0xB6, 0xD9, gradientPulse), ToAbgr(0x2B, 0x1F, 0x33)),
            "Royal Gradient" => (BlendToAbgr(0x8C, 0x7A, 0xF0, 0xF1, 0xC9, 0x5A, gradientPulse), ToAbgr(0x2D, 0x24, 0x14)),
            "Aurora Gradient" => (BlendToAbgr(0x7D, 0xE8, 0xC8, 0x8B, 0xB7, 0xFF, gradientPulse), ToAbgr(0x1A, 0x28, 0x31)),
            "Peach Fizz Gradient" => (BlendToAbgr(0xFF, 0xC2, 0xA1, 0xFF, 0x8C, 0xB3, gradientPulse), ToAbgr(0x3B, 0x23, 0x2A)),
            "Moonlit Gradient" => (BlendToAbgr(0x9A, 0xB3, 0xE8, 0xD7, 0xE2, 0xFF, gradientPulse), ToAbgr(0x1F, 0x26, 0x36)),
            "Ember Gradient" => (BlendToAbgr(0xFF, 0x9B, 0x5A, 0xD6, 0x44, 0x44, gradientPulse), ToAbgr(0x3A, 0x1E, 0x16)),
            "Ocean Dusk Gradient" => (BlendToAbgr(0x64, 0xC8, 0xE8, 0x5B, 0x79, 0xD8, gradientPulse), ToAbgr(0x1A, 0x23, 0x3A)),
            "Spring Bloom Gradient" => (BlendToAbgr(0x9F, 0xE6, 0xA8, 0xFF, 0xB8, 0xD6, gradientPulse), ToAbgr(0x2A, 0x2F, 0x1E)),
            "Crystal Sky Gradient" => (BlendToAbgr(0xB6, 0xF2, 0xFF, 0x96, 0xC2, 0xFF, gradientPulse), ToAbgr(0x1B, 0x2D, 0x38)),
            "Stardust Gradient" => (BlendToAbgr(0xD7, 0xC0, 0xFF, 0xFF, 0xD7, 0xA8, gradientPulse), ToAbgr(0x2E, 0x24, 0x33)),
            "Festival Gradient" => (BlendToAbgr(0xFF, 0x90, 0xB7, 0x8E, 0xD5, 0xFF, gradientPulse), ToAbgr(0x2A, 0x23, 0x34)),
            "Scholar Gradient" => (BlendToAbgr(0xF2, 0xF2, 0xF2, 0xC9, 0xDF, 0xFF, gradientPulse), ToAbgr(0x2A, 0x2A, 0x2E)),
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

    private static bool IsGradientPreset(string preset)
    {
        return !string.IsNullOrWhiteSpace(preset) &&
               preset.Contains("Gradient", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveLocalColorPreset(string title)
    {
        var fallback = this.plugin.Configuration.RewardTitleColorPreset;
        if (!this.plugin.Configuration.EnablePerTitleStyleProfiles || string.IsNullOrWhiteSpace(title))
        {
            return fallback;
        }

        if (this.plugin.Configuration.TitleStyleProfiles.TryGetValue(title.Trim(), out var mapped) &&
            !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        return fallback;
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
