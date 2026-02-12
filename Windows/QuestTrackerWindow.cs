using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using SocialMorpho.Data;
using System.IO;
using System.Numerics;
using System.Linq;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private Plugin Plugin;
    private QuestManager QuestManager;
    private readonly ITextureProvider TextureProvider;
    private IDalamudTextureWrap? CustomQuestIcon;

    // FFXIV color scheme
    private readonly Vector4 FFXIVGold = new(0.83f, 0.69f, 0.22f, 1.0f);      // #D4AF37
    private readonly Vector4 FFXIVCyan = new(0.0f, 0.81f, 0.82f, 1.0f);       // #00CED1

    // Progress bar colors
    private readonly Vector4 ProgressComplete = new(0.3f, 0.9f, 0.3f, 1.0f);  // Bright Green
    private readonly Vector4 ProgressActive = new(0.0f, 0.81f, 0.82f, 0.8f);  // FFXIV Cyan
    private readonly Vector4 ProgressInactive = new(0.4f, 0.4f, 0.4f, 0.6f);  // Dark Gray

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager, ITextureProvider textureProvider) 
        : base("Quest Tracker##SocialMorphoTracker", 
               ImGuiWindowFlags.NoTitleBar | 
               ImGuiWindowFlags.NoResize | 
               ImGuiWindowFlags.AlwaysAutoResize | 
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Plugin = plugin;
        QuestManager = questManager;
        TextureProvider = textureProvider;
        LoadCustomIcon();

        // Position will be set in PreDraw to ensure accurate screen size
        PositionCondition = ImGuiCond.FirstUseEver;

        // Semi-transparent background
        BgAlpha = 0.75f;
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

            var iconPath = Path.Combine(
                assemblyDir,
                "Resources",
                "quest_icon.png"
            );

            if (File.Exists(iconPath))
            {
                var file = new FileInfo(iconPath);
                CustomQuestIcon = TextureProvider.GetFromFile(file).GetWrapOrDefault();
                
                if (CustomQuestIcon != null && CustomQuestIcon.ImGuiHandle != IntPtr.Zero)
                {
                    Plugin.PluginLog.Info($"Custom quest icon loaded from: {iconPath}");
                }
                else
                {
                    Plugin.PluginLog.Warning($"Custom quest icon loaded but handle is invalid");
                }
            }
            else
            {
                Plugin.PluginLog.Warning($"Custom quest icon not found at: {iconPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    public override void PreDraw()
    {
        // Only show if configured to show tracker
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            IsOpen = false;
            return;
        }

        // Set position based on current screen size (only on first use)
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
        {
            // Don't show window if no active quests
            return;
        }

        // Header
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVGold);
        ImGui.Text("Active Quests");
        ImGui.PopStyleColor();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Draw each active quest
        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        // Draw custom quest icon
        DrawCustomIcon();
        
        // Quest title in FFXIV gold
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVGold);
        ImGui.TextWrapped(quest.Title);
        ImGui.PopStyleColor();

        // Objective/Description with arrow symbol (indented)
        ImGui.Indent(20f);
        
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVCyan);
        ImGui.Text("►");
        ImGui.SameLine();
        
        // Show description if available, otherwise show generic objective
        var objectiveText = !string.IsNullOrEmpty(quest.Description) 
            ? quest.Description 
            : $"Complete {quest.GoalCount} objectives";
        
        ImGui.TextWrapped(objectiveText);
        ImGui.PopStyleColor();

        // Progress counter in cyan
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVCyan);
        ImGui.Text($"({quest.CurrentCount}/{quest.GoalCount})");
        ImGui.PopStyleColor();

        // Progress bar (subtle, FFXIV-style)
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        var progressColor = GetProgressBarColor(progress);
        
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(260, 3), "");
        ImGui.PopStyleColor();

        ImGui.Unindent(20f);

        // Spacing between quest entries
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawCustomIcon()
    {
        if (CustomQuestIcon != null)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            
            // Draw icon shadow (subtle black shadow behind)
            drawList.AddImage(
                CustomQuestIcon.ImGuiHandle,
                new Vector2(pos.X + 1, pos.Y + 1),
                new Vector2(pos.X + 21, pos.Y + 21),
                Vector2.Zero,
                Vector2.One,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f))
            );
            
            // Draw main icon
            ImGui.Image(CustomQuestIcon.ImGuiHandle, new Vector2(20, 20));
            ImGui.SameLine();
        }
        else
        {
            // Fallback: show exclamation mark if icon fails to load
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            
            // Shadow
            drawList.AddText(
                new Vector2(pos.X + 1, pos.Y + 1),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)),
                "❗"
            );
            
            // Main text
            ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "❗");
            ImGui.SameLine();
        }
    }

    private Vector4 GetProgressBarColor(float progress)
    {
        if (progress >= 1.0f)
            return ProgressComplete;    // Bright Green for 100%
        else if (progress > 0f)
            return ProgressActive;      // FFXIV Cyan for in-progress
        else
            return ProgressInactive;    // Dark Gray for not started
    }

    public void Dispose()
    {
        CustomQuestIcon?.Dispose();
    }
}
