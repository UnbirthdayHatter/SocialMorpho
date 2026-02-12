using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window, IDisposable
{
    // FFXIV color constants
    private static readonly Vector4 FFXIVGold = new Vector4(0.83f, 0.69f, 0.22f, 1.0f);
    private static readonly Vector4 FFXIVCyan = new Vector4(0.0f, 0.81f, 0.82f, 1.0f);
    private static readonly Vector4 FFXIVCyanAlpha = new Vector4(0.0f, 0.81f, 0.82f, 0.8f);
    private static readonly Vector4 BrightGreen = new Vector4(0.3f, 0.9f, 0.3f, 1.0f);
    private static readonly Vector4 DarkGray = new Vector4(0.4f, 0.4f, 0.4f, 0.6f);
    
    // Quest type indicator colors
    private static readonly Vector4 SocialBlue = new Vector4(0.4f, 0.8f, 1.0f, 1.0f);
    private static readonly Vector4 BuffGreen = new Vector4(0.8f, 1.0f, 0.4f, 1.0f);
    private static readonly Vector4 EmoteOrange = new Vector4(1.0f, 0.8f, 0.4f, 1.0f);
    
    private Plugin Plugin;
    private QuestManager QuestManager;

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager) 
        : base("Quest Tracker##QuestTrackerWindow", 
               ImGuiWindowFlags.NoTitleBar | 
               ImGuiWindowFlags.NoResize | 
               ImGuiWindowFlags.AlwaysAutoResize |
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Plugin = plugin;
        QuestManager = questManager;

        // Position in top-right corner
        Position = new Vector2(ImGui.GetIO().DisplaySize.X - 350, 50);
        PositionCondition = ImGuiCond.FirstUseEver;

        // Semi-transparent background with FFXIV-style tint
        BgAlpha = 0.75f;

        // Show by default if configured
        IsOpen = Plugin.Configuration.ShowQuestTracker;
    }

    public override void PreDraw()
    {
        // Update visibility based on configuration
        IsOpen = Plugin.Configuration.ShowQuestTracker;
    }

    public override void Draw()
    {
        var activeQuests = QuestManager.GetActiveQuests();

        if (activeQuests.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No active quests");
            return;
        }

        // FFXIV-style header with golden color
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVGold);
        ImGui.Text("Active Quests");
        ImGui.PopStyleColor();
        
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing(); // Extra spacing for FFXIV style

        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
            ImGui.Spacing();
            ImGui.Spacing(); // Extra spacing between quest entries
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        // Quest type indicator (colored symbol)
        var typeSymbol = GetQuestTypeSymbol(quest.Type);
        var typeColor = GetQuestTypeIndicatorColor(quest.Type);
        ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
        ImGui.Text(typeSymbol);
        ImGui.PopStyleColor();
        
        ImGui.SameLine();
        
        // Quest title with FFXIV golden color
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVGold);
        ImGui.TextWrapped(quest.Title);
        ImGui.PopStyleColor();

        // Objective/Description text with arrow and cyan color (indented)
        ImGui.Indent(20f);
        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVCyan);
        
        // Arrow symbol before objective
        ImGui.Text($"► {quest.Description}");
        
        // Progress counter in cyan
        ImGui.SameLine();
        ImGui.Text($" ({quest.CurrentCount}/{quest.GoalCount})");
        ImGui.PopStyleColor();
        ImGui.Unindent(20f);

        // Progress bar (subtle, matching FFXIV style)
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        var progressColor = GetProgressColor(progress);
        ImGui.Indent(20f);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(260, 3), string.Empty);
        ImGui.PopStyleColor();
        ImGui.Unindent(20f);
    }

    private string GetQuestTypeSymbol(QuestType type)
    {
        return type switch
        {
            QuestType.Social => "●",    // Circle for social
            QuestType.Buff => "◆",      // Diamond for buff
            QuestType.Emote => "■",     // Square for emote
            QuestType.Custom => "★",    // Star for custom
            _ => "●"
        };
    }

    private Vector4 GetQuestTypeIndicatorColor(QuestType type)
    {
        return type switch
        {
            QuestType.Social => SocialBlue,
            QuestType.Buff => BuffGreen,
            QuestType.Emote => EmoteOrange,
            QuestType.Custom => FFXIVGold,
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }

    private Vector4 GetProgressColor(float progress)
    {
        if (progress >= 1.0f)
            return BrightGreen;  // Complete
        else if (progress > 0f)
            return FFXIVCyanAlpha;  // In progress (FFXIV style)
        else
            return DarkGray;  // Not started
    }

    public void Dispose()
    {
    }
}
