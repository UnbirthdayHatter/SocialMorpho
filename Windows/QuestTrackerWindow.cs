using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
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
    private readonly Vector4 GlowColor = new(0f, 0f, 0f, 0.70f);

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

            var handleProperty = this.CustomQuestIcon.GetType().GetProperty("ImGuiHandle");
            if (handleProperty?.GetValue(this.CustomQuestIcon) is ImTextureID textureHandle)
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
        foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!method.Name.Contains("Image", StringComparison.OrdinalIgnoreCase) &&
                !method.Name.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                continue;

            attempts.Add($"{target.GetType().Name}.{method.Name}(string)");
            var result = TryInvokeMethod(target, method, iconPath);
            if (result != null)
                return result;
        }

        return null;
    }

    private static object? TryInvokeMethod(object target, MethodInfo method, string iconPath)
    {
        try
        {
            var result = method.Invoke(target, new object[] { iconPath });
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
        this.DrawCustomIcon();
        this.DrawGlowText(quest.Title, this.FFXIVGold, true);

        ImGui.Indent(20f);
        this.DrawGlowText("\u25BA", this.FFXIVBlue, false);
        ImGui.SameLine(0f, 6f);

        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";

        this.DrawGlowText(objectiveText, this.FFXIVBlue, true);
        this.DrawGlowText($"({quest.CurrentCount}/{quest.GoalCount})", this.FFXIVBlue, false);
        ImGui.Unindent(20f);
        ImGui.Spacing();
    }

    private void DrawGlowText(string text, Vector4 color, bool wrap)
    {
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var glowU32 = ImGui.ColorConvertFloat4ToU32(this.GlowColor);

        drawList.AddText(new Vector2(pos.X + 1, pos.Y + 0), glowU32, text);
        drawList.AddText(new Vector2(pos.X - 1, pos.Y + 0), glowU32, text);
        drawList.AddText(new Vector2(pos.X + 0, pos.Y + 1), glowU32, text);
        drawList.AddText(new Vector2(pos.X + 0, pos.Y - 1), glowU32, text);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        if (wrap)
            ImGui.TextWrapped(text);
        else
            ImGui.Text(text);
        ImGui.PopStyleColor();
    }

    private void DrawCustomIcon()
    {
        if (this.HasCustomQuestIcon)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            drawList.AddImage(
                this.CustomQuestIconHandle,
                new Vector2(pos.X + 1, pos.Y + 1),
                new Vector2(pos.X + 21, pos.Y + 21),
                Vector2.Zero,
                Vector2.One,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)));

            ImGui.Image(this.CustomQuestIconHandle, new Vector2(20, 20));
            ImGui.SameLine(0f, 6f);
            return;
        }

        var fallbackPos = ImGui.GetCursorScreenPos();
        var fallbackDrawList = ImGui.GetWindowDrawList();

        fallbackDrawList.AddText(
            new Vector2(fallbackPos.X + 1, fallbackPos.Y + 1),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)),
            "!");

        ImGui.TextColored(this.FFXIVGold, "!");
        ImGui.SameLine(0f, 6f);
    }

    public void Dispose()
    {
        if (this.CustomQuestIcon is IDisposable disposable)
            disposable.Dispose();
    }
}
