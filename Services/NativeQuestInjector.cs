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

            // Free previously allocated strings
            FreeAllocatedStrings();

            // Inject each active quest (max 10)
            int questIndex = 0;
            foreach (var quest in activeQuests)
            {
                if (questIndex >= 10) break; // FFXIV supports max 10 quests

                // Allocate quest title string
                var titlePtr = AllocateString(quest.Title);
                
                // Allocate objective string with progress
                var objectiveText = $"{quest.Description} ({quest.CurrentCount}/{quest.GoalCount})";
                var objectivePtr = AllocateString(objectiveText);

                // Set quest icon based on type
                int iconId = GetQuestIcon(quest.Type);

                // Set quest title in string array
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

                questIndex++;
            }

            // Update total quest count in the number array
            if (questIndex > 0)
            {
                numberArray->IntArray[0] = questIndex;
            }

            Plugin.PluginLog.Info($"Injected {questIndex} custom quests into native ToDoList");
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