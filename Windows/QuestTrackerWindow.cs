using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using System.Threading.Tasks;

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
    private object? DefaultFontHandle;

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
            object? loaded = TryInvokeUiImageLoader(uiBuilder, uiBuilderType, iconPath);

            CustomQuestIcon = loaded;
            if (CustomQuestIcon == null)
            {
                Plugin.PluginLog.Warning($"Unable to load custom icon. UiBuilder type: {uiBuilderType.FullName}. Looked for LoadImage/LoadImageRaw runtime methods.");
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

        PushDefaultGameFont();
        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
        }
        PopDefaultGameFont();
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

    private object? TryInvokeUiImageLoader(object uiBuilder, Type uiBuilderType, string iconPath)
    {
        var attempted = new List<string>();
        var fileBytes = File.ReadAllBytes(iconPath);

        // Instance methods first.
        foreach (var method in uiBuilderType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!method.Name.Contains("Image", StringComparison.OrdinalIgnoreCase))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                attempted.Add($"{method.DeclaringType?.FullName}::{method.Name}(string)");
                if (TryInvokeAndUnwrap(method, uiBuilder, new object[] { iconPath }, out var loaded) && loaded != null)
                    return loaded;
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(byte[]))
            {
                attempted.Add($"{method.DeclaringType?.FullName}::{method.Name}(byte[])");
                if (TryInvokeAndUnwrap(method, uiBuilder, new object[] { fileBytes }, out var loaded) && loaded != null)
                    return loaded;
            }
        }

        // Static extension-like methods as fallback.
        foreach (var method in AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(SafeGetTypes)
                     .Where(t => t.IsSealed && t.IsAbstract)
                     .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)))
        {
            if (!method.Name.Contains("Image", StringComparison.OrdinalIgnoreCase))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length == 2
                && parameters[0].ParameterType.IsAssignableFrom(uiBuilderType)
                && parameters[1].ParameterType == typeof(string))
            {
                attempted.Add($"{method.DeclaringType?.FullName}::{method.Name}(IUiBuilder,string)");
                if (TryInvokeAndUnwrap(method, null, new object[] { uiBuilder, iconPath }, out var loaded) && loaded != null)
                    return loaded;
            }
            else if (parameters.Length == 2
                     && parameters[0].ParameterType.IsAssignableFrom(uiBuilderType)
                     && parameters[1].ParameterType == typeof(byte[]))
            {
                attempted.Add($"{method.DeclaringType?.FullName}::{method.Name}(IUiBuilder,byte[])");
                if (TryInvokeAndUnwrap(method, null, new object[] { uiBuilder, fileBytes }, out var loaded) && loaded != null)
                    return loaded;
            }
        }

        Plugin.PluginLog.Warning($"No compatible image loader succeeded. Attempted: {string.Join(" | ", attempted.Distinct())}");
        return null;
    }

    private static bool TryInvokeAndUnwrap(MethodInfo method, object? target, object[] args, out object? loaded)
    {
        loaded = null;
        try
        {
            var result = method.Invoke(target, args);
            loaded = UnwrapTaskLikeResult(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? UnwrapTaskLikeResult(object? result)
    {
        if (result == null)
            return null;

        if (result is Task task)
        {
            task.GetAwaiter().GetResult();
            var resultProp = task.GetType().GetProperty("Result");
            return resultProp?.GetValue(task);
        }

        // ValueTask<T> or other awaitable-like wrappers with Result property.
        var prop = result.GetType().GetProperty("Result");
        return prop != null ? prop.GetValue(result) : result;
    }

    private void PushDefaultGameFont()
    {
        try
        {
            var uiBuilder = Plugin.PluginInterface.UiBuilder;
            var defaultFontProp = uiBuilder.GetType().GetProperty("DefaultFontHandle");
            DefaultFontHandle = defaultFontProp?.GetValue(uiBuilder);
            if (DefaultFontHandle == null)
                return;

            var pushMethod = DefaultFontHandle.GetType().GetMethod("Push", BindingFlags.Public | BindingFlags.Instance);
            pushMethod?.Invoke(DefaultFontHandle, null);
        }
        catch
        {
            DefaultFontHandle = null;
        }
    }

    private void PopDefaultGameFont()
    {
        try
        {
            if (DefaultFontHandle == null)
                return;

            var popMethod = DefaultFontHandle.GetType().GetMethod("Pop", BindingFlags.Public | BindingFlags.Instance);
            popMethod?.Invoke(DefaultFontHandle, null);
        }
        catch
        {
            // Ignore and continue rendering.
        }
        finally
        {
            DefaultFontHandle = null;
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
