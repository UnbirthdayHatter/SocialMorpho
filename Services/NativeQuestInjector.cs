using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SocialMorpho.Data;

namespace SocialMorpho.Services;

public unsafe class NativeQuestInjector : IDisposable
{
    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private readonly List<IntPtr> AllocatedStrings = new();
    
    // Quest type icons (use FFXIV icon IDs)
    private const int SocialQuestIcon = 61412;  // Blue dot icon
    private const int BuffQuestIcon = 61413;    // Green dot icon  
    private const int EmoteQuestIcon = 61414;   // Orange dot icon
    
    public NativeQuestInjector(Plugin plugin, QuestManager questManager)
    {
        Plugin = plugin;
        QuestManager = questManager;
        
        // Subscribe to framework update events for injection
        Plugin.PluginInterface.UiBuilder.FrameworkUpdate += OnFrameworkUpdate;
    }
    
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Plugin.Configuration.ShowQuestTracker) return;
        
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
        var stringArray = RaptureTextModule.Instance()->GetStringArrayData(RaptureTextModule.StringArrayType.ToDoList);
        var numberArray = RaptureTextModule.Instance()->GetNumberArrayData(RaptureTextModule.NumberArrayType.ToDoList);
        
        if (stringArray == null || numberArray == null) return;
        
        var activeQuests = QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0) return;
        
        // Get current native quest count from offset 8 (QuestCount)
        int nativeQuestCount = numberArray->IntArray[8];
        int totalQuests = Math.Min(nativeQuestCount + activeQuests.Count, 10);
        
        // Calculate starting index for our quests
        int customQuestStartIndex = nativeQuestCount;
        
        // Free previously allocated strings
        FreeAllocatedStrings();
        
        // Inject each custom quest
        for (int i = 0; i < activeQuests.Count && customQuestStartIndex < 10; i++)
        {
            var quest = activeQuests[i];
            int questIndex = customQuestStartIndex + i;
            
            // Calculate text array indices
            // Offset 9 to 69 contains quest texts (60 entries total)
            // Format: titles first, then objectives/details
            int titleIndex = 9 + questIndex;
            int objectiveIndex = 9 + 10 + questIndex; // Objectives start after all title slots
            
            // Allocate and set quest title
            var titlePtr = AllocateString(quest.Title);
            stringArray->StringArray[titleIndex] = (byte*)titlePtr;
            
            // Allocate and set quest objective with progress
            var objectiveText = $"= {quest.Description}  ({quest.CurrentCount}/{quest.GoalCount})";
            var objectivePtr = AllocateString(objectiveText);
            stringArray->StringArray[objectiveIndex] = (byte*)objectivePtr;
            
            // Set quest metadata in number array
            // QuestTypeIcon at offset 9
            numberArray->IntArray[9 + questIndex] = GetQuestIcon(quest.Type);
            
            // ObjectiveCountForQuest at offset 19
            numberArray->IntArray[19 + questIndex] = 1;
            
            // ObjectiveProgress at offset 29
            numberArray->IntArray[29 + questIndex] = quest.CurrentCount;
            
            // ButtonCountForQuest at offset 39
            numberArray->IntArray[39 + questIndex] = 0;
        }
        
        // Update total quest count at offset 8
        numberArray->IntArray[8] = totalQuests;
        
        // Enable quest list at offset 7
        numberArray->IntArray[7] = 1;
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
        Plugin.PluginInterface.UiBuilder.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllocatedStrings();
    }
}
