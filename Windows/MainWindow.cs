using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System;
using System.Numerics;

namespace SocialMorpho.Windows;


public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private QuestManager QuestManager;

    public MainWindow(Plugin plugin, QuestManager questManager) : base("Social Morpho##MainWindow")
    {
        Plugin = plugin;
        QuestManager = questManager;

        Size = new Vector2(400, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

    }

    public override void Draw()
    {
        DrawQuestList();
        DrawSettings();
    }

    private void DrawQuestList()
    {
        ImGui.Text("Active Quests");
        ImGui.Separator();

        if (ImGui.BeginChild("##QuestListChild", new Vector2(0, 400)))
        {
            var quests = QuestManager.GetAllQuests();
            foreach (var quest in quests)
            {
                DrawQuestItem(quest);
            }
            ImGui.EndChild();
        }
    }

    private void DrawQuestItem(QuestData quest)
    {
        ImGui.TextWrapped($"[{quest.Id}] {quest.Title}");
        ImGui.SameLine();

        if (ImGui.Button($"Details##details{quest.Id}"))
        {
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset##reset{quest.Id}"))
        {
            QuestManager.ResetQuestProgress(quest.Id);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Complete##complete{quest.Id}"))
        {
            QuestManager.MarkQuestComplete(quest.Id);
        }

        ImGui.Spacing();
    }

    private void DrawSettings()
    {
        ImGui.Separator();
        ImGui.Text("Settings");

        bool soundEnabled = Plugin.Configuration.SoundEnabled;
        bool compactMode = Plugin.Configuration.CompactMode;
        float panelOpacity = Plugin.Configuration.PanelOpacity;

        if (ImGui.Checkbox("Sound Enabled", ref soundEnabled))
        {
            Plugin.Configuration.SoundEnabled = soundEnabled;
        }

        if (ImGui.SliderFloat("Panel Opacity", ref panelOpacity, 0.0f, 1.0f))
        {
            Plugin.Configuration.PanelOpacity = panelOpacity;
        }

        if (ImGui.Checkbox("Compact Mode", ref compactMode))
        {
            Plugin.Configuration.CompactMode = compactMode;
        }

        if (ImGui.Button("Save Settings"))
        {
            Plugin.Configuration.Save();
        }
    }

    public void Dispose()
    {
    }
}