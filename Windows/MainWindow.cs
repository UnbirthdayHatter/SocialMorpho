using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using SocialMorpho.Data;
using System;
using System.Numerics;

namespace SocialMorpho.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;
    private string NewQuestTitle = string.Empty;
    private string NewQuestDescription = string.Empty;
    private int NewQuestGoal = 1;
    private int SelectedQuestType = 0;
    private bool ShowAddQuestModal = false;

    public MainWindow(Plugin plugin, QuestManager questManager) : base("Social Morpho##MainWindow")
    {
        Plugin = plugin;
        QuestManager = questManager;

        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(420, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

        QuestManager.OnQuestsChanged += OnQuestsChanged;
    }

    public override void Draw()
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize - new Vector2(450, 20), ImGuiCond.FirstUseEver, new Vector2(1, 1));

        ImGui.BeginChild("QuestPanel", new Vector2(-1, -40), true);

        DrawQuests();

        ImGui.EndChild();

        ImGui.Spacing();
        if (ImGui.Button("+ Add Social Quest", new Vector2(-1, 30)))
        {
            ShowAddQuestModal = true;
        }

        if (ShowAddQuestModal)
        {
            DrawAddQuestModal();
        }
    }

    private void DrawQuests()
    {
        var quests = QuestManager.GetAllQuests();

        if (quests.Count == 0)
        {
            ImGui.TextDisabled("No quests available");
            return;
        }

        foreach (var quest in quests)
        {
            DrawQuestItem(quest);
            ImGui.Spacing();
        }
    }

    private void DrawQuestItem(QuestData quest)
    {
        ImGui.PushID((int)quest.Id);

        var bgColor = quest.Completed ? new Vector4(0.2f, 0.5f, 0.2f, 0.3f) : new Vector4(0.2f, 0.2f, 0.3f, 0.3f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, bgColor);
        ImGui.BeginChild($"Quest_{quest.Id}", new Vector2(-1, 100), true);

        // Title
        var titleColor = quest.Completed ? new Vector4(0.5f, 1, 0.5f, 1) : new Vector4(1, 0.84f, 0.3f, 1);
        ImGui.PushStyleColor(ImGuiCol.Text, titleColor);
        ImGui.TextWrapped(quest.Title);
        ImGui.PopStyleColor();

        // Description
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1));
        ImGui.TextWrapped(quest.Description);
        ImGui.PopStyleColor();

        // Progress bar
        float progress = quest.GoalCount > 0 ? (float)quest.CurrentCount / quest.GoalCount : 0;
        ImGui.ProgressBar(progress, new Vector2(-1, 20), $"{quest.CurrentCount}/{quest.GoalCount}");

        // Buttons
        ImGui.Spacing();
        float buttonWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2) / 3;

        if (ImGui.Button($"+##Progress_{quest.Id}", new Vector2(buttonWidth, 0)))
        {
            QuestManager.UpdateQuestProgress(quest.Id);
        }

        ImGui.SameLine();
        if (ImGui.Button($"↻##Reset_{quest.Id}", new Vector2(buttonWidth, 0)))
        {
            QuestManager.ResetQuest(quest.Id);
        }

        ImGui.SameLine();
        if (ImGui.Button($"✕##Delete_{quest.Id}", new Vector2(buttonWidth, 0)))
        {
            QuestManager.DeleteQuest(quest.Id);
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopID();
    }

    private void DrawAddQuestModal()
    {
        ImGui.OpenPopupOnItemClick("AddQuestPopup", ImGuiPopupFlags.MouseButtonLeft);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("Add Social Quest", ref ShowAddQuestModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.InputText("Quest Title##NewQuestTitle", ref NewQuestTitle, 100);
            ImGui.InputTextMultiline("Description##NewQuestDescription", ref NewQuestDescription, 200, new Vector2(300, 60));
            ImGui.InputInt("Goal Count##NewQuestGoal", ref NewQuestGoal);

            if (NewQuestGoal < 1) NewQuestGoal = 1;

            ImGui.Combo("Quest Type##NewQuestType", ref SelectedQuestType, new[] { "Social", "Buff", "Emote", "Custom" }, 4);

            ImGui.Spacing();

            if (ImGui.Button("Create", new Vector2(100, 0)))
            {
                if (!string.IsNullOrWhiteSpace(NewQuestTitle))
                {
                    QuestManager.AddQuest(
                        NewQuestTitle,
                        NewQuestDescription,
                        NewQuestGoal,
                        (QuestType)SelectedQuestType
                    );

                    // Reset form
                    NewQuestTitle = string.Empty;
                    NewQuestDescription = string.Empty;
                    NewQuestGoal = 1;
                    SelectedQuestType = 0;
                    ShowAddQuestModal = false;
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 0)))
            {
                ShowAddQuestModal = false;
            }

            ImGui.EndPopup();
        }
    }

    private void OnQuestsChanged()
    {
        PluginLog.Information("Quests updated in Social Morpho");
    }

    public void Dispose()
    {
        QuestManager.OnQuestsChanged -= OnQuestsChanged;
    }
}