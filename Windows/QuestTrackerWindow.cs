using Dalamud.Interface.Windowing;
using ImGuiNET;
using SocialMorpho.Data;
using System;
using System.Numerics;
using System.Linq;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;

    // FFXIV color scheme
    private readonly Vector4 FFXIVGold = new(0.83f, 0.69f, 0.22f, 1.0f);      // #D4AF37
    private readonly Vector4 FFXIVCyan = new(0.0f, 0.81f, 0.82f, 1.0f);       // #00CED1
    private readonly Vector4 SocialColor = new(0.4f, 0.8f, 1.0f, 1.0f);       // Light Blue
    private readonly Vector4 BuffColor = new(0.8f, 1.0f, 0.4f, 1.0f);         // Light Green
    private readonly Vector4 EmoteColor = new(1.0f, 0.8f, 0.4f, 1.0f);        // Light Orange
    private readonly Vector4 CustomColor = new(0.83f, 0.69f, 0.22f, 1.0f);    // FFXIV Gold

    // Progress bar colors
    private readonly Vector4 ProgressComplete = new(0.3f, 0.9f, 0.3f, 1.0f);  // Bright Green
    private readonly Vector4 ProgressActive = new(0.0f, 0.81f, 0.82f, 0.8f);  // FFXIV Cyan
    private readonly Vector4 ProgressInactive = new(0.4f, 0.4f, 0.4f, 0.6f);  // Dark Gray

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager) 
        : base("Quest Tracker##SocialMorphoTracker", 
               ImGuiWindowFlags.NoTitleBar | 
               ImGuiWindowFlags.NoResize | 
               ImGuiWindowFlags.AlwaysAutoResize | 
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Plugin = plugin;
        QuestManager = questManager;

        // Position in top-right area (will be adjusted based on screen size)
        Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        // Semi-transparent background
        BgAlpha = 0.75f;
    }

    public override void PreDraw()
    {
        // Only show if configured to show tracker
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            IsOpen = false;
            return;
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
        // Quest type indicator symbol with color
        var typeSymbol = GetQuestTypeSymbol(quest.Type);
        var typeColor = GetQuestTypeColor(quest.Type);
        
        ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
        ImGui.Text(typeSymbol);
        ImGui.PopStyleColor();
        
        ImGui.SameLine();
        
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

    private string GetQuestTypeSymbol(QuestType type)
    {
        return type switch
        {
            QuestType.Social => "●",   // Circle
            QuestType.Buff => "◆",     // Diamond
            QuestType.Emote => "■",    // Square
            QuestType.Custom => "★",   // Star
            _ => "●"
        };
    }

    private Vector4 GetQuestTypeColor(QuestType type)
    {
        return type switch
        {
            QuestType.Social => SocialColor,
            QuestType.Buff => BuffColor,
            QuestType.Emote => EmoteColor,
            QuestType.Custom => CustomColor,
            _ => FFXIVGold
        };
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
        // Nothing to dispose
    }
}
