using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
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
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (raptureAtkModule == null) return;

        var activeQuests = QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0) return;

        // Access the ToDoList addon
        var addon = (AddonToDoList*)Plugin.PluginInterface.UiBuilder.GetAddonByName("_ToDoList");
        if (addon == null || !addon->AtkUnitBase.IsVisible) return;

        // Get the string and number arrays for ToDoList
        // These array IDs are specific to the ToDoList addon
        var stringArrayData = raptureAtkModule->GetStringArrayData(72); // ToDoList string array
        var numberArrayData = raptureAtkModule->GetNumberArrayData(73); // ToDoList number array
        
        if (stringArrayData == null || numberArrayData == null) return;

        // Get the current number of native quests
        // The first element in the number array typically contains metadata about the list
        var nativeQuestCount = numberArrayData->IntArray[0];
        
        // Free previously allocated strings to prevent memory leaks
        FreeAllocatedStrings();

        // Calculate how many custom quests we can inject (max 10 total)
        var maxQuests = 10;
        var availableSlots = maxQuests - nativeQuestCount;
        var questsToInject = Math.Min(activeQuests.Count, availableSlots);

        if (questsToInject <= 0) return;

        // Starting index for custom quests (after native quests)
        var startIndex = nativeQuestCount;

        // Inject each custom quest
        for (int i = 0; i < questsToInject; i++)
        {
            var quest = activeQuests[i];
            var questIndex = startIndex + i;

            // Build quest title string
            var titlePtr = AllocateString(quest.Title);
            
            // Build objective string with progress
            var objectiveText = $"{quest.Description} ({quest.CurrentCount}/{quest.GoalCount})";
            var objectivePtr = AllocateString(objectiveText);

            // Get quest icon based on type
            var iconId = GetQuestIcon(quest.Type);

            // Set quest title in string array (title index)
            stringArrayData->SetValue(questIndex * 3, (byte*)titlePtr, false, true, false);
            
            // Set quest objective in string array (objective index)
            stringArrayData->SetValue(questIndex * 3 + 1, (byte*)objectivePtr, false, true, false);

            // Set quest icon ID in number array
            numberArrayData->IntArray[questIndex * 10 + 1] = iconId;
            
            // Set objective count
            numberArrayData->IntArray[questIndex * 10 + 2] = 1;
            
            // Set current progress
            numberArrayData->IntArray[questIndex * 10 + 3] = quest.CurrentCount;
            
            // Set goal count
            numberArrayData->IntArray[questIndex * 10 + 4] = quest.GoalCount;
        }

        // Update total quest count in the number array
        numberArrayData->IntArray[0] = nativeQuestCount + questsToInject;
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