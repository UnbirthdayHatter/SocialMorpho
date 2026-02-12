using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private const float ContainerWidth = 300f;
    private const float IconSize = 26f;
    private const float IconGap = 2f;
    private const float RightPadding = 10f;

    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private ISharedImmediateTexture? CustomQuestIcon;
    private string? LoadedIconPath;
    private bool LoggedWrapFailure;

    private readonly Vector4 TitleColor = new(0.898f, 0.765f, 0.514f, 1.0f); // #E5C383
    private readonly Vector4 BodyColor = new(0.41f, 0.80f, 0.95f, 1.0f);
    private readonly Vector4 ShadowColor = new(0f, 0f, 0f, 0.55f);

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

        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - RightPadding;
        var leftEdge = rightEdge - ContainerWidth;
        var iconWidth = this.CustomQuestIcon != null ? IconSize : 12f;
        var textRightEdge = rightEdge - iconWidth - IconGap;
        var maxTextWidth = MathF.Max(60f, textRightEdge - leftEdge);

        const float titleScale = 1.04f;
        const float bodyScale = 1.00f;

        var y = MathF.Round(ImGui.GetCursorScreenPos().Y);

        var titleLines = this.WrapToWidth(quest.Title, maxTextWidth, titleScale);
        foreach (var line in titleLines)
        {
            var pos = this.SetCursorForRightAlignedText(line, textRightEdge, y, titleScale);
            this.DrawShadowedText(line, this.TitleColor, pos, titleScale);
            y += ImGui.GetTextLineHeight() * titleScale;
        }

        this.DrawCustomIconAt(rightEdge - iconWidth, MathF.Round(ImGui.GetCursorScreenPos().Y));

        y += 3f;
        var objectiveLines = this.WrapToWidth(objectiveText, maxTextWidth, bodyScale);
        foreach (var line in objectiveLines)
        {
            var pos = this.SetCursorForRightAlignedText(line, textRightEdge, y, bodyScale);
            this.DrawShadowedText(line, this.BodyColor, pos, bodyScale);
            y += ImGui.GetTextLineHeight() * bodyScale;
        }

        y += 1f;
        var progressText = $"{quest.CurrentCount}/{quest.GoalCount}";
        var progressPos = this.SetCursorForRightAlignedText(progressText, textRightEdge, y, bodyScale);
        this.DrawShadowedText(progressText, this.BodyColor, progressPos, bodyScale);

        y += ImGui.GetTextLineHeight() * bodyScale + 6f;
        ImGui.SetCursorScreenPos(new Vector2(leftEdge, y));
        ImGui.Spacing();
    }

    private Vector2 SetCursorForRightAlignedText(string text, float rightEdgeX, float y, float scale)
    {
        var textSize = ImGui.CalcTextSize(text) * scale;
        var leftX = MathF.Max(ImGui.GetWindowPos().X + 6f, rightEdgeX - textSize.X);
        leftX = MathF.Round(leftX);
        y = MathF.Round(y);
        ImGui.SetCursorScreenPos(new Vector2(leftX, y));
        return new Vector2(leftX, y);
    }

    private void DrawShadowedText(string text, Vector4 textColor, Vector2 textPos, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var shadowU32 = ImGui.ColorConvertFloat4ToU32(this.ShadowColor);
        drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 1), shadowU32, text);

        ImGui.SetWindowFontScale(scale);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
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
            drawListWrap.Draw(drawList, new Vector2(x, y), new Vector2(x + IconSize, y + IconSize));
            return;
        }

        if (this.CustomQuestIcon != null &&
            this.CustomQuestIcon.TryGetWrap(out var wrapForHandle, out _) &&
            TryGetImGuiTextureFromWrap(wrapForHandle, out var textureId))
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.Image(textureId, new Vector2(IconSize, IconSize));
            return;
        }

        if (this.CustomQuestIcon != null && !this.LoggedWrapFailure)
        {
            this.LoggedWrapFailure = true;
            this.Plugin.PluginLog.Warning(
                $"Icon loaded but TryGetWrap/IDrawListTextureWrap path failed. Icon path: {this.LoadedIconPath ?? "<unknown>"}; Icon type: {this.CustomQuestIcon.GetType().FullName}");
        }

        var fallbackDrawList = ImGui.GetWindowDrawList();
        fallbackDrawList.AddText(new Vector2(x, y), ImGui.ColorConvertFloat4ToU32(this.TitleColor), "!");
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
            .Where(m =>
                (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                m.ReturnType == textureType &&
                m.GetParameters().Length == 1);
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

        foreach (var ctor in textureType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
        {
            var p = ctor.GetParameters();
            if (p.Length != 1)
                continue;

            try
            {
                var arg = p[0].ParameterType.IsAssignableFrom(valueType) ? value : Convert.ChangeType(value, p[0].ParameterType);
                var created = ctor.Invoke(new[] { arg });
                if (created is ImTextureID typed)
                {
                    textureId = typed;
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

    private List<string> WrapToWidth(string text, float maxWidth, float scale)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Add(string.Empty);
            return result;
        }

        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var current = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = $"{current} {words[i]}";
                var width = ImGui.CalcTextSize(candidate).X * scale;
                if (width <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = words[i];
                }
            }

            result.Add(current);
        }

        return result;
    }

    public void Dispose()
    {
    }
}
