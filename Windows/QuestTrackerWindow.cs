using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.IO;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private ISharedImmediateTexture? CustomQuestIcon;
    private string? LoadedIconPath;
    private bool LoggedWrapFailure;

    // Tuned close to native quest glow tones.
    private readonly Vector4 FFXIVGold = new(0.77f, 0.69f, 0.44f, 0.42f);
    private readonly Vector4 FFXIVBlue = new(0.45f, 0.76f, 0.90f, 0.40f);
    private readonly Vector4 WhiteText = new(1.0f, 1.0f, 1.0f, 1.0f);

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager)
        : base("Quest Tracker##SocialMorphoTracker",
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.AlwaysAutoResize |
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.Plugin = plugin;
        this.QuestManager = questManager;

        this.LoadCustomIcon();
        this.PositionCondition = ImGuiCond.FirstUseEver;
        this.BgAlpha = 0.0f;
    }

    private void LoadCustomIcon()
    {
        try
        {
            var assemblyDir = this.Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrEmpty(assemblyDir))
                return;

            var candidates = new[]
            {
                Path.Combine(assemblyDir, "Resources", "quest_icon.png"),
                Path.Combine(assemblyDir, "Resources", "LoveQuest.png"),
                Path.Combine(assemblyDir, "LoveQuest.png"),
                Path.Combine(assemblyDir, "quest_icon.png"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;

                var texture = this.Plugin.TextureProvider.GetFromFile(path);
                if (texture == null)
                    continue;

                this.CustomQuestIcon = texture;
                this.LoadedIconPath = path;
                this.Plugin.PluginLog.Info($"Custom quest icon loaded from: {path}");
                return;
            }

            this.Plugin.PluginLog.Warning($"Custom quest icon not found or failed to load. Tried: {string.Join(", ", candidates)}");
        }
        catch (Exception ex)
        {
            this.Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    public override void PreDraw()
    {
        if (!this.Plugin.Configuration.ShowQuestTracker)
        {
            this.IsOpen = false;
            return;
        }

        if (!this.Position.HasValue)
            this.Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);

        base.PreDraw();
    }

    public override void Draw()
    {
        var activeQuests = this.QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0)
            return;

        foreach (var quest in activeQuests)
            this.DrawQuestEntry(quest);
    }

    private void DrawQuestEntry(QuestData quest)
    {
        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";

        var baseCursor = ImGui.GetCursorScreenPos();
        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - 16f;
        var iconWidth = this.CustomQuestIcon != null ? 20f : 12f;
        var lineHeight = ImGui.GetTextLineHeight();
        var titleY = baseCursor.Y;
        var objectiveY = titleY + lineHeight + 2f;
        var progressY = objectiveY + lineHeight + 1f;
        var nextEntryY = progressY + lineHeight + 6f;

        const float titleScale = 1.10f;
        var titlePos = this.SetCursorForRightAlignedText(quest.Title, rightEdge - iconWidth - 6f, titleY, titleScale);
        this.DrawHaloText(quest.Title, this.FFXIVGold, titlePos, titleScale);
        this.DrawCustomIconAt(rightEdge - iconWidth, titlePos.Y);

        var objectivePos = this.SetCursorForRightAlignedText(objectiveText, rightEdge, objectiveY);
        this.DrawHaloText(objectiveText, this.FFXIVBlue, objectivePos);

        var progressText = $"{quest.CurrentCount}/{quest.GoalCount}";
        var progressPos = this.SetCursorForRightAlignedText(progressText, rightEdge, progressY);
        this.DrawHaloText(progressText, this.FFXIVBlue, progressPos);

        ImGui.SetCursorScreenPos(new Vector2(baseCursor.X, nextEntryY));
        ImGui.Spacing();
    }

    private Vector2 SetCursorForRightAlignedText(string text, float rightEdgeX, float y, float scale = 1.0f)
    {
        var textSize = ImGui.CalcTextSize(text) * scale;
        var leftX = MathF.Max(ImGui.GetWindowPos().X + 6f, rightEdgeX - textSize.X);
        leftX = MathF.Round(leftX);
        y = MathF.Round(y);
        ImGui.SetCursorScreenPos(new Vector2(leftX, y));
        return new Vector2(leftX, y);
    }

    private void DrawHaloText(string text, Vector4 haloColor, Vector2 textPos, float scale = 1.0f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var haloU32 = ImGui.ColorConvertFloat4ToU32(haloColor);

        // Crisp single halo (4-neighbor), no secondary blur pass.
        drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X - 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y + 1), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y - 1), haloU32, text);

        ImGui.SetWindowFontScale(scale);
        ImGui.PushStyleColor(ImGuiCol.Text, this.WhiteText);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);
    }

    private void DrawCustomIconAt(float x, float y)
    {
        x = MathF.Round(x);
        y = MathF.Round(y);

        if (this.CustomQuestIcon != null &&
            this.CustomQuestIcon.TryGetWrap(out var wrap, out _) &&
            wrap is IDrawListTextureWrap drawListWrap)
        {
            var drawList = ImGui.GetWindowDrawList();
            drawListWrap.Draw(drawList, new Vector2(x, y), new Vector2(x + 20f, y + 20f));
            return;
        }

        if (this.CustomQuestIcon != null && !this.LoggedWrapFailure)
        {
            this.LoggedWrapFailure = true;
            this.Plugin.PluginLog.Warning(
                $"Icon loaded but TryGetWrap/IDrawListTextureWrap path failed. Icon path: {this.LoadedIconPath ?? "<unknown>"}; Icon type: {this.CustomQuestIcon.GetType().FullName}");
        }

        var fallbackDrawList = ImGui.GetWindowDrawList();
        fallbackDrawList.AddText(new Vector2(x, y), ImGui.ColorConvertFloat4ToU32(this.FFXIVGold), "!");
    }

    public void Dispose()
    {
    }
}
