using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private const float TrackerWidth = 380f;
    private const float TitleWrapWidth = 320f;
    private const float ObjectiveWrapWidth = 300f;

    private Plugin Plugin;
    private QuestManager QuestManager;
    private object? CustomQuestIcon;
    private ImTextureID CustomQuestIconHandle;
    private bool HasCustomQuestIcon;

    // FFXIV color scheme
    private readonly Vector4 FFXIVWhite = new(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly Vector4 FFXIVGoldHalo = new(0.98f, 0.80f, 0.40f, 0.85f);
    private readonly Vector4 FFXIVBlueHalo = new(0.20f, 0.90f, 1.0f, 0.85f);

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager)
        : base("Quest Tracker##SocialMorphoTracker",
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.NoBackground |
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Plugin = plugin;
        QuestManager = questManager;
        LoadCustomIcon();

        // Position will be set in PreDraw to ensure accurate screen size
        PositionCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(TrackerWidth, 0f);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Semi-transparent background
        BgAlpha = 0.0f;
    }

    private void LoadCustomIcon()
    {
        try
        {
            var assemblyDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrEmpty(assemblyDir))
            {
                Plugin.PluginLog.Warning("Assembly directory path is null or empty, cannot load custom quest icon");
                return;
            }

            var iconPath = Path.Combine(assemblyDir, "Resources", "quest_icon.png");

            // Development fallback to legacy location.
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(assemblyDir, "LoveQuest.png");
            }

            if (!File.Exists(iconPath))
            {
                Plugin.PluginLog.Warning($"Custom quest icon not found at: {iconPath}");
                return;
            }

            var uiBuilder = Plugin.PluginInterface.UiBuilder;
            var uiBuilderType = uiBuilder.GetType();
            var loadImageMethod = uiBuilderType
                .GetMethods()
                .FirstOrDefault(m => m.Name == "LoadImage"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string));

            object? loaded = null;

            if (loadImageMethod != null)
            {
                loaded = loadImageMethod.Invoke(uiBuilder, new object[] { iconPath });
            }
            else
            {
                // Fallback: look for extension-style static LoadImage(IUiBuilder, string)
                var extensionMethod = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(SafeGetTypes)
                    .Where(t => t.IsSealed && t.IsAbstract)
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "LoadImage") return false;
                        var p = m.GetParameters();
                        return p.Length == 2
                            && p[0].ParameterType.IsAssignableFrom(uiBuilderType)
                            && p[1].ParameterType == typeof(string);
                    });

                if (extensionMethod != null)
                {
                    loaded = extensionMethod.Invoke(null, new object[] { uiBuilder, iconPath });
                }
            }

            CustomQuestIcon = loaded;
            if (CustomQuestIcon == null)
            {
                Plugin.PluginLog.Warning($"Unable to load custom icon. UiBuilder type: {uiBuilderType.FullName}");
                return;
            }

            if (TryExtractTextureHandle(CustomQuestIcon, out var textureHandle))
            {
                CustomQuestIconHandle = textureHandle;
                HasCustomQuestIcon = true;
                Plugin.PluginLog.Info($"Custom quest icon loaded from: {iconPath}");
            }
            else
            {
                Plugin.PluginLog.Warning($"Loaded icon did not expose a usable texture handle for path: {iconPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    public override void PreDraw()
    {
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            IsOpen = false;
            return;
        }

        if (!Position.HasValue)
        {
            Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);
        }

        base.PreDraw();
    }

    public override void Draw()
    {
        var activeQuests = QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0)
            return;

        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        var entryStart = ImGui.GetCursorScreenPos();
        var rightEdge = entryStart.X + ImGui.GetContentRegionAvail().X - 4f;
        const float iconSize = 20f;
        const float iconGap = 8f;

        var titleRightEdge = rightEdge - iconSize - iconGap;
        var titleHeight = DrawHaloWrappedTextRightAligned(quest.Title, TitleWrapWidth, titleRightEdge, FFXIVWhite, FFXIVGoldHalo);
        DrawCustomIconAt(rightEdge - iconSize, entryStart.Y, iconSize);

        ImGui.SetCursorScreenPos(new Vector2(entryStart.X, entryStart.Y + MathF.Max(titleHeight, iconSize) + 2f));
        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";
        var objectiveLine = $"{objectiveText}  {quest.CurrentCount}/{quest.GoalCount}";
        DrawHaloWrappedTextRightAligned(objectiveLine, ObjectiveWrapWidth, rightEdge, FFXIVWhite, FFXIVBlueHalo);
        ImGui.Spacing();
    }

    private void DrawCustomIconAt(float x, float y, float iconSize)
    {
        if (HasCustomQuestIcon)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = new Vector2(x, y);

            drawList.AddImage(
                CustomQuestIconHandle,
                new Vector2(pos.X + 1, pos.Y + 1),
                new Vector2(pos.X + iconSize + 1, pos.Y + iconSize + 1),
                Vector2.Zero,
                Vector2.One,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f))
            );

            drawList.AddImage(
                CustomQuestIconHandle,
                pos,
                new Vector2(pos.X + iconSize, pos.Y + iconSize),
                Vector2.Zero,
                Vector2.One
            );
            return;
        }

        var fallbackPos = new Vector2(x, y);
        var fallbackDrawList = ImGui.GetWindowDrawList();

        fallbackDrawList.AddText(
            new Vector2(fallbackPos.X + 1, fallbackPos.Y + 1),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)),
            "!"
        );
        fallbackDrawList.AddText(
            fallbackPos,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.84f, 0.0f, 1.0f)),
            "!"
        );
    }

    private float DrawHaloWrappedTextRightAligned(string text, float wrapWidth, float rightEdgeX, Vector4 textColor, Vector4 haloColor)
    {
        var lines = WrapText(text, wrapWidth);
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var main = ImGui.ColorConvertFloat4ToU32(textColor);
        var halo = ImGui.ColorConvertFloat4ToU32(haloColor);

        for (int i = 0; i < lines.Count; i++)
        {
            var lineWidth = ImGui.CalcTextSize(lines[i]).X;
            var linePos = new Vector2(rightEdgeX - lineWidth, start.Y + (i * lineHeight));
            drawList.AddText(new Vector2(linePos.X - 1, linePos.Y), halo, lines[i]);
            drawList.AddText(new Vector2(linePos.X + 1, linePos.Y), halo, lines[i]);
            drawList.AddText(new Vector2(linePos.X, linePos.Y - 1), halo, lines[i]);
            drawList.AddText(new Vector2(linePos.X, linePos.Y + 1), halo, lines[i]);
            drawList.AddText(linePos, main, lines[i]);
        }

        ImGui.Dummy(new Vector2(wrapWidth, lines.Count * lineHeight));
        return lines.Count * lineHeight;
    }

    private List<string> WrapText(string text, float maxWidth)
    {
        var result = new List<string>();
        var words = text.Split(' ');
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (ImGui.CalcTextSize(candidate).X <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
            }
            else
            {
                result.Add(current);
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
            result.Add(current);

        if (result.Count == 0)
            result.Add(string.Empty);

        return result;
    }

    private bool TryExtractTextureHandle(object imageObject, out ImTextureID handle)
    {
        handle = default;

        // Common property names in different Dalamud versions/wrappers.
        var candidateProperties = new[] { "ImGuiHandle", "Handle", "TextureHandle" };
        foreach (var propertyName in candidateProperties)
        {
            var property = imageObject.GetType().GetProperty(propertyName);
            if (property == null)
                continue;

            var value = property.GetValue(imageObject);
            if (TryConvertToImTextureId(value, out handle))
                return true;
        }

        // Some APIs may return the handle directly.
        if (TryConvertToImTextureId(imageObject, out handle))
            return true;

        return false;
    }

    private bool TryConvertToImTextureId(object? value, out ImTextureID handle)
    {
        handle = default;
        if (value == null)
            return false;

        switch (value)
        {
            case ImTextureID id:
                handle = id;
                return true;
            case ulong ul:
                handle = (ImTextureID)ul;
                return ul != 0;
            case uint ui:
                handle = (ImTextureID)(ulong)ui;
                return ui != 0;
            case int i:
                if (i <= 0) return false;
                handle = (ImTextureID)(ulong)i;
                return true;
            case IntPtr ptr:
                {
                    var raw = ptr.ToInt64();
                    if (raw <= 0) return false;
                    handle = (ImTextureID)(ulong)raw;
                    return true;
                }
            default:
                return false;
        }
    }

    public void Dispose()
    {
        if (CustomQuestIcon is IDisposable disposable)
            disposable.Dispose();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}
