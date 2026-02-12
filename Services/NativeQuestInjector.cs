using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SocialMorpho.Data;

namespace SocialMorpho.Services;

public unsafe class NativeQuestInjector : IDisposable
{
    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private bool IsDisposed = false;

    private const int SocialQuestIcon = 61412;
    private const int BuffQuestIcon = 61413;
    private const int EmoteQuestIcon = 61414;

    public NativeQuestInjector(Plugin plugin, QuestManager questManager)
    {
        Plugin = plugin;
        QuestManager = questManager;
        Plugin.PluginInterface.UiBuilder.Draw += OnUpdate;
    }

    private void OnUpdate()
    {
        if (IsDisposed || !Plugin.Configuration.ShowQuestTracker) return;

        try
        {
            InjectCustomQuests();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to inject quests: {ex}");
        }
    }

    private void InjectCustomQuests()
    {
        try
        {
            var activeQuests = QuestManager.GetActiveQuests();
            if (activeQuests.Count == 0) return;

            // Access FFXIV's ToDoList arrays using the modern approach
            var stringArray = AtkStage.Instance()->GetStringArrayData(StringArrayType.ToDoList);
            if (stringArray == null) return;

            // Get number array using the proper method
            var numberArray = AtkStage.Instance()->GetNumberArrayData(NumberArrayType.ToDoList);
            if (numberArray == null) return;
            
            var todoNumberArray = (ToDoListNumberArray*)numberArray->IntArray;

            // Get the current number of native quests from the correct field
            var nativeQuestCount = todoNumberArray->QuestCount;

            // Calculate how many custom quests we can inject (max 10 total)
            var maxQuests = 10;
            var availableSlots = maxQuests - nativeQuestCount;
            var questsToInject = Math.Min(activeQuests.Count, availableSlots);

            if (questsToInject <= 0) return;

            // Starting index for custom quests (after native quests)
            var startIndex = nativeQuestCount;

            // Inject each active quest
            int injectedCount = 0;
            for (int i = 0; i < questsToInject; i++)
            {
                var quest = activeQuests[i];
                var questSlot = startIndex + i;

                // Build objective string with progress
                var objectiveText = $"{quest.Description} ({quest.CurrentCount}/{quest.GoalCount})";

                // Set quest icon based on type
                int iconId = GetQuestIcon(quest.Type);

                // TODO: Native quest string injection requires InteropGenerator.Runtime dependency
                // For now, quest display uses QuestTrackerWindow.cs ImGui overlay
                // Future: Research proper StringArrayData manipulation without SetValue()
                Plugin.PluginLog.Debug($"Quest {questSlot}: {quest.Title} - {objectiveText}");

                // Set quest icon using the correct field
                todoNumberArray->QuestTypeIcon[questSlot] = iconId;
                
                // Set objective count
                todoNumberArray->ObjectiveCountForQuest[questSlot] = 1;
                
                // Set current progress
                todoNumberArray->ObjectiveProgress[questSlot] = quest.CurrentCount;
                
                // Set button count
                todoNumberArray->ButtonCountForQuest[questSlot] = 0;

                injectedCount++;
            }

            // Update total quest count
            todoNumberArray->QuestCount = nativeQuestCount + injectedCount;
            
            // Enable quest list
            todoNumberArray->QuestListEnabled = true;

            Plugin.PluginLog.Info($"Native quest injection infrastructure ready for {injectedCount} quests (display via overlay)");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to inject quests: {ex}");
        }
    }

    private int GetQuestIcon(QuestType type)
    {
        return type switch
        {
            QuestType.Social => SocialQuestIcon,
            QuestType.Buff => BuffQuestIcon,
            QuestType.Emote => EmoteQuestIcon,
            _ => SocialQuestIcon
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Plugin.PluginInterface.UiBuilder.Draw -= OnUpdate;
    }
}