using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
    private readonly List<IntPtr> AllocatedStrings = new();
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

            var numberArray = ToDoListNumberArray.Instance();
            if (numberArray == null) return;

            // Get the current number of native quests
            var nativeQuestCount = numberArray->IntArray[0];

            // Calculate how many custom quests we can inject (max 10 total)
            var maxQuests = 10;
            var availableSlots = maxQuests - nativeQuestCount;
            var questsToInject = Math.Min(activeQuests.Count, availableSlots);

            if (questsToInject <= 0) return;

            // Free previously allocated strings
            FreeAllocatedStrings();

            // Starting index for custom quests (after native quests)
            var startIndex = nativeQuestCount;

            // Inject each active quest
            int injectedCount = 0;
            for (int i = 0; i < questsToInject; i++)
            {
                var quest = activeQuests[i];
                var questIndex = startIndex + i;

                // Build objective string with progress
                var objectiveText = $"{quest.Description} ({quest.CurrentCount}/{quest.GoalCount})";

                // Set quest icon based on type
                int iconId = GetQuestIcon(quest.Type);

                // Set quest title in string array
                // Using managed=true so the game allocates memory
                stringArray->SetValue(questIndex * 3, quest.Title, false, true, false);
                
                // Set quest objective in string array
                stringArray->SetValue(questIndex * 3 + 1, objectiveText, false, true, false);

                // Set quest icon ID in number array
                numberArray->IntArray[questIndex * 10 + 1] = iconId;
                
                // Set objective count
                numberArray->IntArray[questIndex * 10 + 2] = 1;
                
                // Set current progress
                numberArray->IntArray[questIndex * 10 + 3] = quest.CurrentCount;
                
                // Set goal count
                numberArray->IntArray[questIndex * 10 + 4] = quest.GoalCount;

                injectedCount++;
            }

            // Update total quest count in the number array (native + custom)
            numberArray->IntArray[0] = nativeQuestCount + injectedCount;

            Plugin.PluginLog.Info($"Injected {injectedCount} custom quests into native ToDoList (after {nativeQuestCount} native quests)");
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to inject quests: {ex}");
        }
    }

    private IntPtr AllocateString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        AllocatedStrings.Add(ptr);
        return ptr;
    }

    private void FreeAllocatedStrings()
    {
        foreach (var ptr in AllocatedStrings)
        {
            Marshal.FreeHGlobal(ptr);
        }
        AllocatedStrings.Clear();
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
        FreeAllocatedStrings();
    }
}