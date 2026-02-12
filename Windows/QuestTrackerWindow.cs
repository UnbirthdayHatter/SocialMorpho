using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window, IDisposable
{
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

        // Semi-transparent background
        BgAlpha = 0.7f;

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

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.95f, 0.7f, 1.0f));
        ImGui.Text("Active Quests");
        ImGui.PopStyleColor();
        
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        // Quest title with type color
        var titleColor = GetQuestTypeColor(quest.Type);
        ImGui.PushStyleColor(ImGuiCol.Text, titleColor);
        ImGui.TextWrapped(quest.Title);
        ImGui.PopStyleColor();

        // Progress display
        ImGui.SameLine();
        ImGui.Text($"({quest.CurrentCount}/{quest.GoalCount})");

        // Progress bar
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        var progressColor = GetProgressColor(progress);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(280, 4), string.Empty);
        ImGui.PopStyleColor();

        ImGui.Spacing();
    }

    private Vector4 GetQuestTypeColor(QuestType type)
    {
        return type switch
        {
            QuestType.Social => new Vector4(0.4f, 0.8f, 1.0f, 1.0f),    // Light blue
            QuestType.Buff => new Vector4(0.8f, 1.0f, 0.4f, 1.0f),      // Light green
            QuestType.Emote => new Vector4(1.0f, 0.8f, 0.4f, 1.0f),     // Light orange
            QuestType.Custom => new Vector4(0.9f, 0.9f, 0.9f, 1.0f),    // White
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }

    private Vector4 GetProgressColor(float progress)
    {
        if (progress >= 1.0f)
            return new Vector4(0.2f, 0.8f, 0.2f, 1.0f);  // Green - complete
        else if (progress > 0f)
            return new Vector4(0.8f, 0.8f, 0.2f, 1.0f);  // Yellow - in progress
        else
            return new Vector4(0.5f, 0.5f, 0.5f, 1.0f);  // Gray - not started
    }

    public void Dispose()
    {
    }
}
