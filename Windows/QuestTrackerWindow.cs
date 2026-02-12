using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private object? CustomQuestIcon;
    private ImTextureID CustomQuestIconHandle;
    private bool HasCustomQuestIcon;

    // Tuned to match requested FFXIV-like tones.
    private readonly Vector4 FFXIVGold = new(0.7137f, 0.6196f, 0.3765f, 1.0f); // #b69e60
    private readonly Vector4 FFXIVBlue = new(0.3059f, 0.5765f, 0.6941f, 1.0f); // #4e93b1
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

        // Position will be set in PreDraw to ensure accurate screen size.
        this.PositionCondition = ImGuiCond.FirstUseEver;
        this.BgAlpha = 0.0f; // Transparent like native quest UI.
    }

    private void LoadCustomIcon()
    {
        try
        {
            var assemblyDir = this.Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrEmpty(assemblyDir))
            {
                this.Plugin.PluginLog.Warning("Assembly directory path is null or empty, cannot load custom quest icon");
                return;
            }

            var iconPath = Path.Combine(assemblyDir, "Resources", "quest_icon.png");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(assemblyDir, "LoveQuest.png");
            }

            if (!File.Exists(iconPath))
            {
                this.Plugin.PluginLog.Warning($"Custom quest icon not found at: {iconPath}");
                return;
            }

            var attempts = new List<string>();
            this.CustomQuestIcon = this.TryLoadTexture(iconPath, attempts);
            if (this.CustomQuestIcon == null)
            {
                this.Plugin.PluginLog.Warning(
                    $"Unable to load custom icon. UiBuilder type: {this.Plugin.PluginInterface.UiBuilder.GetType().FullName}");
                if (attempts.Count > 0)
                {
                    this.Plugin.PluginLog.Warning($"No compatible image loader succeeded. Attempted: {string.Join(", ", attempts)}");
                }
                return;
            }

            if (this.TryExtractImGuiHandle(this.CustomQuestIcon, out var textureHandle))
            {
                this.CustomQuestIconHandle = textureHandle;
                this.HasCustomQuestIcon = true;
                this.Plugin.PluginLog.Info($"Custom quest icon loaded from: {iconPath}");
            }
            else
            {
                this.Plugin.PluginLog.Warning($"Loaded icon did not expose ImGuiHandle for path: {iconPath}");
            }
        }
        catch (Exception ex)
        {
            this.Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    private object? TryLoadTexture(string iconPath, List<string> attempts)
    {
        // Probe both UiBuilder and PluginInterface for texture methods across Dalamud versions.
        var candidates = new List<object?> { this.Plugin.PluginInterface.UiBuilder, this.Plugin.PluginInterface };

        foreach (var target in candidates)
        {
            if (target == null)
                continue;

            var texture = this.TryInvokeTextureMethods(target, iconPath, attempts);
            if (texture != null)
                return texture;

            foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var name = prop.Name;
                if (!name.Contains("Texture", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Image", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? service;
                try
                {
                    service = prop.GetValue(target);
                }
                catch
                {
                    continue;
                }

                if (service == null)
                    continue;

                texture = this.TryInvokeTextureMethods(service, iconPath, attempts);
                if (texture != null)
                    return texture;
            }
        }

        return null;
    }

    private object? TryInvokeTextureMethods(object target, string iconPath, List<string> attempts)
    {
        var iconBytes = File.ReadAllBytes(iconPath);

        foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!method.Name.Contains("Image", StringComparison.OrdinalIgnoreCase) &&
                !method.Name.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = method.GetParameters();
            var args = BuildLoaderArgs(parameters, iconPath, iconBytes);
            if (args == null)
                continue;

            attempts.Add($"{target.GetType().Name}.{method.Name}({string.Join(",", parameters.Select(p => p.ParameterType.Name))})");
            var result = TryInvokeMethod(target, method, args);
            if (result != null)
                return result;
        }

        return null;
    }

    private static object?[]? BuildLoaderArgs(ParameterInfo[] parameters, string iconPath, byte[] iconBytes)
    {
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            if (type == typeof(string))
            {
                args[i] = iconPath;
                continue;
            }

            if (type == typeof(byte[]))
            {
                args[i] = iconBytes;
                continue;
            }

            if (type == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            if (type == typeof(bool))
            {
                args[i] = true;
                continue;
            }

            if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
                continue;
            }

            return null;
        }

        return args;
    }

    private static object? TryInvokeMethod(object target, MethodInfo method, object?[] args)
    {
        try
        {
            var result = method.Invoke(target, args);
            if (result == null)
                return null;

            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private bool TryExtractImGuiHandle(object textureObject, out ImTextureID textureHandle)
    {
        var handleProperties = textureObject.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var property in handleProperties)
        {
            object? value;
            try
            {
                value = property.GetValue(textureObject);
            }
            catch
            {
                continue;
            }

            if (TryCreateTextureIdFromValue(value, out var id))
            {
                textureHandle = id;
                return true;
            }
        }

        textureHandle = default;
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
        var constructors = textureType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var ctorParams = ctor.GetParameters();
            if (ctorParams.Length != 1)
                continue;

            var targetType = ctorParams[0].ParameterType;
            object? argValue = null;
            var canConvert = false;

            if (targetType.IsAssignableFrom(valueType))
            {
                argValue = value;
                canConvert = true;
            }
            else
            {
                try
                {
                    argValue = Convert.ChangeType(value, targetType);
                    canConvert = true;
                }
                catch
                {
                }
            }

            if (!canConvert)
                continue;

            try
            {
                var created = ctor.Invoke(new[] { argValue });
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

    public override void PreDraw()
    {
        if (!this.Plugin.Configuration.ShowQuestTracker)
        {
            this.IsOpen = false;
            return;
        }

        if (!this.Position.HasValue)
        {
            this.Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);
        }

        base.PreDraw();
    }

    public override void Draw()
    {
        var activeQuests = this.QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0)
            return;

        foreach (var quest in activeQuests)
        {
            this.DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";

        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - 8f;
        var iconWidth = this.HasCustomQuestIcon ? 20f : 12f;
        var lineSpacing = 2f;

        var titlePos = this.SetCursorForRightAlignedText(quest.Title, rightEdge - iconWidth - 6f);
        this.DrawHaloText(quest.Title, this.FFXIVGold, titlePos);
        this.DrawCustomIconAt(rightEdge - iconWidth, titlePos.Y);

        var objectivePos = this.SetCursorForRightAlignedText(objectiveText, rightEdge);
        this.DrawHaloText(objectiveText, this.FFXIVBlue, objectivePos);

        var progressText = $"{quest.CurrentCount}/{quest.GoalCount}";
        var progressPos = this.SetCursorForRightAlignedText(progressText, rightEdge);
        this.DrawHaloText(progressText, this.FFXIVBlue, progressPos);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + lineSpacing);
        ImGui.Spacing();
    }

    private Vector2 SetCursorForRightAlignedText(string text, float rightEdgeX)
    {
        var textSize = ImGui.CalcTextSize(text);
        var cursorScreen = ImGui.GetCursorScreenPos();
        var leftX = MathF.Max(ImGui.GetWindowPos().X + 6f, rightEdgeX - textSize.X);
        var localX = leftX - ImGui.GetWindowPos().X;
        ImGui.SetCursorPosX(localX);
        return new Vector2(leftX, cursorScreen.Y);
    }

    private void DrawHaloText(string text, Vector4 haloColor, Vector2 textPos)
    {
        var drawList = ImGui.GetWindowDrawList();
        var haloU32 = ImGui.ColorConvertFloat4ToU32(haloColor);
        drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X - 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y + 1), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y - 1), haloU32, text);

        ImGui.PushStyleColor(ImGuiCol.Text, this.WhiteText);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void DrawCustomIconAt(float x, float y)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        if (this.HasCustomQuestIcon)
        {
            ImGui.Image(this.CustomQuestIconHandle, new Vector2(20, 20));
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(this.FFXIVGold), "!");
    }

    public void Dispose()
    {
        if (this.CustomQuestIcon is IDisposable disposable)
            disposable.Dispose();
    }
}
