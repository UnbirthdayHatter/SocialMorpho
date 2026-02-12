using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;
using System.Linq;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;

    // FFXIV-style colors
    private static readonly Vector4 FFXIVGold = new(0.83f, 0.69f, 0.22f, 1.0f);
    private static readonly Vector4 FFXIVCyan = new(0.0f, 0.81f, 0.82f, 1.0f);
    private static readonly Vector4 CompleteGreen = new(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Vector4 InProgressCyan = new(0.0f, 0.81f, 0.82f, 1.0f);
    private static readonly Vector4 NotStartedGray = new(0.5f, 0.5f, 0.5f, 1.0f);
    
    // Layout constants
    private const float WindowOffsetFromRight = 350f;
    private const float WindowTopPosition = 100f;
    private const float QuestDetailsIndentSize = 20f;
    private const float ProgressBarWidth = 260f;
    private const float ProgressBarHeight = 3f;
    
    private bool _positionInitialized = false;

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager) 
        : base("Quest Tracker##SocialMorphoTracker", 
               ImGuiWindowFlags.NoTitleBar | 
               ImGuiWindowFlags.NoResize | 
               ImGuiWindowFlags.AlwaysAutoResize | 
               ImGuiWindowFlags.NoFocusOnAppearing |
               ImGuiWindowFlags.NoScrollbar)
    {
        Plugin = plugin;
        QuestManager = questManager;

        // Position will be set on first draw when ImGui context is available
        PositionCondition = ImGuiCond.FirstUseEver;
        
        // Semi-transparent background
        BgAlpha = 0.75f;
    }

    public override void Draw()
    {
        // Initialize position on first draw when ImGui context is available
        if (!_positionInitialized)
        {
            Position = new Vector2(ImGui.GetIO().DisplaySize.X - WindowOffsetFromRight, WindowTopPosition);
            _positionInitialized = true;
        }
        
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            IsOpen = false;
            return;
        }

        var activeQuests = QuestManager.GetActiveQuests();
        
        if (activeQuests.Count == 0)
        {
            ImGui.TextColored(FFXIVCyan, "No active quests");
            return;
        }

        foreach (var quest in activeQuests)
        {
            DrawQuest(quest);
            ImGui.Spacing();
            ImGui.Spacing();
        }
    }

    private void DrawQuest(QuestData quest)
    {
        // Quest type indicator symbol
        string typeSymbol = quest.Type switch
        {
            QuestType.Social => "●",
            QuestType.Buff => "◆",
            QuestType.Emote => "■",
            QuestType.Custom => "★",
            _ => "●"
        };

        // Quest type color
        Vector4 typeColor = quest.Type switch
        {
            QuestType.Social => new Vector4(0.5f, 0.7f, 1.0f, 1.0f),   // Light blue
            QuestType.Buff => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),     // Light green
            QuestType.Emote => new Vector4(1.0f, 0.7f, 0.4f, 1.0f),    // Light orange
            QuestType.Custom => new Vector4(1.0f, 1.0f, 1.0f, 1.0f),   // White
            _ => FFXIVGold
        };

        // Draw quest title with type indicator
        ImGui.TextColored(typeColor, typeSymbol);
        ImGui.SameLine();
        ImGui.TextColored(FFXIVGold, quest.Title);

        // Indent for quest details
        ImGui.Indent(QuestDetailsIndentSize);

        // Draw description with arrow
        ImGui.TextColored(FFXIVCyan, $"► {quest.Description}");

        // Draw progress counter
        string progressText = $"{quest.CurrentCount}/{quest.GoalCount}";
        ImGui.TextColored(FFXIVCyan, progressText);

        // Draw progress bar
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0f;
        
        // Determine progress bar color
        Vector4 progressColor;
        if (progress >= 1.0f)
            progressColor = CompleteGreen;
        else if (progress > 0f)
            progressColor = InProgressCyan;
        else
            progressColor = NotStartedGray;

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, progressColor);
        ImGui.ProgressBar(progress, new Vector2(ProgressBarWidth, ProgressBarHeight), string.Empty);
        ImGui.PopStyleColor();

        ImGui.Unindent(QuestDetailsIndentSize);
    }

    public override void OnClose()
    {
        // Don't actually close, just hide if user clicks X
        Plugin.Configuration.ShowQuestTracker = false;
        Plugin.Configuration.Save();
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
