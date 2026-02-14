using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using SocialMorpho.Data;
using SocialMorpho.Services;
using System.IO;
using System.Numerics;

namespace SocialMorpho.Windows;

public sealed class QuestBoardWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly QuestBoardService questBoardService;
    private string boardStatus = string.Empty;
    private ISharedImmediateTexture? boardTrimTexture;
    private bool triedLoadTrimTexture;
    private static readonly string[] TrimPresets = new[] { "Tight", "Balanced", "Wide" };

    public QuestBoardWindow(Plugin plugin, QuestBoardService questBoardService)
        : base("Social Morpho Quest Board##QuestBoard")
    {
        this.plugin = plugin;
        this.questBoardService = questBoardService;
        this.Size = new Vector2(780, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        EnsureTrimTextureLoaded();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.10f, 0.08f, 0.82f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.62f, 0.50f, 0.30f, 0.65f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.55f, 0.45f, 0.30f, 0.60f));

        var trimInfo = DrawBoardTrimStrip();
        ImGui.TextColored(new Vector4(0.93f, 0.83f, 0.63f, 1f), "Quest Board");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.75f, 0.70f, 0.62f, 1f), "Daily optional side-quests");
        ImGui.SameLine();
        DrawTrimPresetSelector(trimInfo);
        ImGui.Separator();

        var offers = this.questBoardService.GetAvailableOffers();
        var seasonalOffers = this.questBoardService.GetAvailableSeasonalOffers();
        var activeSeason = this.questBoardService.GetActiveSeasonLabel();
        var activeSeasonCountdown = this.questBoardService.GetActiveSeasonCountdownLabel();
        var upcomingSeason = this.questBoardService.GetUpcomingSeasonPreviewLabel();
        if (offers.Count == 0 && seasonalOffers.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.78f, 0.74f, 0.68f, 1f), "No offers left today. Check back after daily reset.");
            ImGui.PopStyleColor(3);
            return;
        }

        if (!string.IsNullOrWhiteSpace(this.boardStatus))
        {
            ImGui.TextColored(new Vector4(0.86f, 0.83f, 0.72f, 1f), this.boardStatus);
            ImGui.Separator();
        }

        if (ImGui.BeginChild("##QuestBoardOffers", new Vector2(0, 0), true))
        {
            if (!string.IsNullOrWhiteSpace(activeSeason))
            {
                ImGui.TextColored(new Vector4(0.98f, 0.78f, 0.86f, 1f), $"Seasonal Track: {activeSeason}");
                if (!string.IsNullOrWhiteSpace(activeSeasonCountdown))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.90f, 0.84f, 0.76f, 1f), $"({activeSeasonCountdown})");
                }
                ImGui.Separator();
                foreach (var offer in seasonalOffers)
                {
                    DrawOfferCard(offer);
                }

                if (seasonalOffers.Count == 0)
                {
                    ImGui.TextDisabled("No seasonal offers left today.");
                    ImGui.Spacing();
                }

                ImGui.Separator();
            }
            else if (!string.IsNullOrWhiteSpace(upcomingSeason))
            {
                ImGui.TextColored(new Vector4(0.82f, 0.80f, 0.90f, 1f), $"Upcoming Season: {upcomingSeason}");
                ImGui.Separator();
            }

            ImGui.TextColored(new Vector4(0.87f, 0.82f, 0.72f, 1f), "Board Contracts");
            ImGui.Separator();
            foreach (var offer in offers)
            {
                DrawOfferCard(offer);
            }

            ImGui.EndChild();
        }

        ImGui.PopStyleColor(3);
    }

    private void DrawOfferCard(QuestBoardOffer offer)
    {
        ImGui.PushID(offer.OfferId);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.13f, 0.10f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.Border, GetRarityBorderGlow(offer.RarityLabel));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4.0f);
        if (ImGui.BeginChild("##OfferCard", new Vector2(0f, 132f), true))
        {
            DrawQuestTypeBadge(offer.QuestType);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.95f, 0.89f, 0.76f, 1f), offer.Title);
            ImGui.SameLine();
            ImGui.TextColored(GetRarityColor(offer.RarityLabel), $"[{offer.RarityLabel}]");

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
            ImGui.TextColored(new Vector4(0.87f, 0.85f, 0.78f, 1f), offer.Description);
            ImGui.PopTextWrapPos();

            ImGui.TextColored(new Vector4(0.68f, 0.72f, 0.80f, 1f), $"Goal: {offer.GoalCount}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.78f, 0.70f, 0.86f, 1f), $"Type: {offer.QuestType}");
            ImGui.SameLine();
            ImGui.TextColored(GetRiskColor(offer.RiskClass), $"Risk: {offer.RiskClass}");

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.30f, 0.37f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.37f, 0.46f, 0.26f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.31f, 0.18f, 1f));
            if (ImGui.Button("Accept Quest", new Vector2(140f, 28f)))
            {
                var accepted = this.questBoardService.AcceptOffer(offer.OfferId);
                this.boardStatus = accepted
                    ? $"Accepted: {offer.Title}"
                    : $"Could not accept offer: {offer.Title}";
            }
            ImGui.PopStyleColor(3);

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.22f, 0.21f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.44f, 0.27f, 0.26f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.29f, 0.18f, 0.17f, 1f));
            if (ImGui.Button("Decline Today", new Vector2(140f, 28f)))
            {
                this.questBoardService.DismissOffer(offer.OfferId);
                this.boardStatus = $"Dismissed today: {offer.Title}";
            }
            ImGui.PopStyleColor(3);
        }
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
        ImGui.PopID();
        ImGui.Spacing();
    }

    private void DrawQuestTypeBadge(QuestType type)
    {
        var badgeColor = type switch
        {
            QuestType.Social => new Vector4(0.35f, 0.62f, 0.82f, 1f),
            QuestType.Buff => new Vector4(0.44f, 0.74f, 0.44f, 1f),
            QuestType.Emote => new Vector4(0.86f, 0.66f, 0.34f, 1f),
            _ => new Vector4(0.66f, 0.58f, 0.82f, 1f),
        };

        var label = type switch
        {
            QuestType.Social => "SO",
            QuestType.Buff => "BU",
            QuestType.Emote => "EM",
            _ => "CU",
        };

        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(24f, 18f);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(badgeColor), 3f);
        dl.AddRect(pos, pos + size, ImGui.GetColorU32(new Vector4(0.95f, 0.91f, 0.80f, 0.95f)), 3f, 0, 1.0f);
        var textSize = ImGui.CalcTextSize(label);
        var textPos = pos + new Vector2((size.X - textSize.X) * 0.5f, (size.Y - textSize.Y) * 0.5f - 1f);
        dl.AddText(textPos, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)), label);
        ImGui.Dummy(new Vector2(size.X, size.Y));
    }

    private static Vector4 GetRarityBorderGlow(string rarity)
    {
        return rarity switch
        {
            "Rare" => new Vector4(0.96f, 0.76f, 0.38f, 0.90f),
            "Uncommon" => new Vector4(0.58f, 0.80f, 0.92f, 0.82f),
            _ => new Vector4(0.60f, 0.49f, 0.30f, 0.75f),
        };
    }

    private TrimRenderInfo DrawBoardTrimStrip()
    {
        var preset = ResolveTrimPreset();
        var (uvMin, uvMax, height) = GetTrimRenderInfo(preset);

        if (this.boardTrimTexture == null)
        {
            return new TrimRenderInfo(preset, uvMin, uvMax, height);
        }

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var end = start + new Vector2(width, height);

        if (!TryDrawTextureCropped(this.boardTrimTexture, start, end, uvMin, uvMax))
        {
            TryDrawTexture(this.boardTrimTexture, start, end);
        }
        ImGui.Dummy(new Vector2(width, height + 2f));
        return new TrimRenderInfo(preset, uvMin, uvMax, height);
    }

    private void DrawTrimPresetSelector(TrimRenderInfo trimInfo)
    {
        var current = ResolveTrimPreset();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo("##QuestBoardTrimPreset", current))
        {
            foreach (var option in TrimPresets)
            {
                var selected = string.Equals(current, option, StringComparison.Ordinal);
                if (ImGui.Selectable(option, selected))
                {
                    this.plugin.Configuration.QuestBoardTrimPreset = option;
                    this.plugin.Configuration.Save();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"h:{trimInfo.Height:0}  uv:{trimInfo.UvMin.Y:0.000}-{trimInfo.UvMax.Y:0.000}");
    }

    private string ResolveTrimPreset()
    {
        var preset = this.plugin.Configuration.QuestBoardTrimPreset;
        if (string.IsNullOrWhiteSpace(preset) || !TrimPresets.Contains(preset, StringComparer.Ordinal))
        {
            return "Balanced";
        }

        return preset;
    }

    private static (Vector2 uvMin, Vector2 uvMax, float height) GetTrimRenderInfo(string preset)
    {
        return preset switch
        {
            "Tight" => (new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.085f), 14f),
            "Wide" => (new Vector2(0.02f, 0.005f), new Vector2(0.98f, 0.16f), 24f),
            _ => (new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.115f), 18f), // Balanced
        };
    }

    private readonly struct TrimRenderInfo
    {
        public TrimRenderInfo(string preset, Vector2 uvMin, Vector2 uvMax, float height)
        {
            Preset = preset;
            UvMin = uvMin;
            UvMax = uvMax;
            Height = height;
        }

        public string Preset { get; }
        public Vector2 UvMin { get; }
        public Vector2 UvMax { get; }
        public float Height { get; }
    }

    private void EnsureTrimTextureLoaded()
    {
        if (this.triedLoadTrimTexture)
        {
            return;
        }

        this.triedLoadTrimTexture = true;
        try
        {
            var assemblyDir = this.plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrWhiteSpace(assemblyDir))
            {
                return;
            }

            var candidates = new[]
            {
                Path.Combine(assemblyDir, "Resources", "aCmbspI.png"),
                Path.Combine(assemblyDir, "Resources", "acmbspi.png"),
            };

            var file = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }

            this.boardTrimTexture = this.plugin.TextureProvider.GetFromFile(file);
        }
        catch
        {
        }
    }

    private static bool TryDrawTexture(ISharedImmediateTexture texture, Vector2 topLeft, Vector2 bottomRight)
    {
        if (texture.TryGetWrap(out var wrap, out _) && wrap is IDrawListTextureWrap drawListWrap)
        {
            drawListWrap.Draw(ImGui.GetWindowDrawList(), topLeft, bottomRight);
            return true;
        }

        if (texture.TryGetWrap(out var wrapForHandle, out _) && TryGetImGuiTextureFromWrap(wrapForHandle, out var textureId))
        {
            ImGui.SetCursorScreenPos(topLeft);
            ImGui.Image(textureId, new Vector2(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));
            return true;
        }

        return false;
    }

    private static bool TryDrawTextureCropped(ISharedImmediateTexture texture, Vector2 topLeft, Vector2 bottomRight, Vector2 uvMin, Vector2 uvMax)
    {
        if (texture.TryGetWrap(out var wrapForHandle, out _) && TryGetImGuiTextureFromWrap(wrapForHandle, out var textureId))
        {
            ImGui.SetCursorScreenPos(topLeft);
            ImGui.Image(textureId, new Vector2(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y), uvMin, uvMax);
            return true;
        }

        return false;
    }

    private static bool TryGetImGuiTextureFromWrap(IDalamudTextureWrap wrap, out ImTextureID textureId)
    {
        try
        {
            var handleValue = wrap.GetType().GetProperty("Handle")?.GetValue(wrap);
            if (TryCreateTextureIdFromValue(handleValue, out var id))
            {
                textureId = id;
                return true;
            }
        }
        catch
        {
        }

        textureId = default;
        return false;
    }

    private static bool TryCreateTextureIdFromValue(object? value, out ImTextureID textureId)
    {
        if (value is ImTextureID direct)
        {
            textureId = direct;
            return true;
        }

        if (value == null)
        {
            textureId = default;
            return false;
        }

        var textureType = typeof(ImTextureID);
        var valueType = value.GetType();
        var operators = textureType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.ReturnType == textureType && m.GetParameters().Length == 1);
        foreach (var op in operators)
        {
            var paramType = op.GetParameters()[0].ParameterType;
            try
            {
                var arg = paramType.IsAssignableFrom(valueType) ? value : Convert.ChangeType(value, paramType);
                var result = op.Invoke(null, new[] { arg });
                if (result is ImTextureID converted)
                {
                    textureId = converted;
                    return true;
                }
            }
            catch
            {
            }
        }

        textureId = default;
        return false;
    }

    private static Vector4 GetRarityColor(string rarity)
    {
        return rarity switch
        {
            "Rare" => new Vector4(0.96f, 0.72f, 0.34f, 1f),
            "Uncommon" => new Vector4(0.64f, 0.83f, 0.93f, 1f),
            _ => new Vector4(0.79f, 0.79f, 0.79f, 1f),
        };
    }

    private static Vector4 GetRiskColor(AntiCheeseRisk risk)
    {
        return risk switch
        {
            AntiCheeseRisk.High => new Vector4(0.92f, 0.52f, 0.47f, 1f),
            AntiCheeseRisk.Medium => new Vector4(0.89f, 0.77f, 0.46f, 1f),
            _ => new Vector4(0.62f, 0.87f, 0.62f, 1f),
        };
    }

    public void Dispose()
    {
    }
}
