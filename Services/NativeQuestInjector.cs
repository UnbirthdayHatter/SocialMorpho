using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<int, IntPtr> OriginalStringPointers = new();
    
    // Quest type icons (use FFXIV icon IDs)
    private const int SocialQuestIcon = 61412;  // Blue dot icon
    private const int BuffQuestIcon = 61413;    // Green dot icon  
    private const int EmoteQuestIcon = 61414;   // Orange dot icon
    
    // ToDoList array offsets
    private const int QuestListEnabledOffset = 7;
    private const int QuestCountOffset = 8;
    private const int QuestTypeIconOffset = 9;
    private const int ObjectiveCountOffset = 19;
    private const int ObjectiveProgressOffset = 29;
    private const int ButtonCountOffset = 39;
    private const int StringArrayStartOffset = 9;
    private const int TitleSlotsCount = 10;
    
    // Formatting constants
    private const string ObjectivePrefix = "= ";
    
    // Caching for optimization
    private List<QuestData>? LastActiveQuests = null;
    private int OriginalQuestCount = 0;
    
    public NativeQuestInjector(Plugin plugin, QuestManager questManager)
    {
        Plugin = plugin;
        QuestManager = questManager;
        
        // Subscribe to framework update events for injection
        Plugin.PluginInterface.UiBuilder.FrameworkUpdate += OnFrameworkUpdate;
    }
    
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            // If quest tracker is disabled, restore original state
            if (LastActiveQuests != null && LastActiveQuests.Count > 0)
            {
                RestoreOriginalState();
            }
            return;
        }
        
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
        var instance = RaptureTextModule.Instance();
        if (instance == null) return;
        
        var stringArray = instance->GetStringArrayData(RaptureTextModule.StringArrayType.ToDoList);
        var numberArray = instance->GetNumberArrayData(RaptureTextModule.NumberArrayType.ToDoList);
        
        if (stringArray == null || numberArray == null) return;
        
        var activeQuests = QuestManager.GetActiveQuests();
        
        // Optimization: Only update if quest list changed
        if (LastActiveQuests != null && QuestsAreEqual(LastActiveQuests, activeQuests))
        {
            return;
        }
        
        // Get current native quest count and store it
        int nativeQuestCount = numberArray->IntArray[QuestCountOffset];
        if (OriginalQuestCount == 0)
        {
            OriginalQuestCount = nativeQuestCount;
        }
        
        // If no custom quests, restore original state
        if (activeQuests.Count == 0)
        {
            if (LastActiveQuests != null && LastActiveQuests.Count > 0)
            {
                RestoreOriginalState();
            }
            LastActiveQuests = new List<QuestData>();
            return;
        }
        
        int totalQuests = Math.Min(nativeQuestCount + activeQuests.Count, 10);
        int customQuestStartIndex = nativeQuestCount;
        
        // Free previously allocated strings
        FreeAllocatedStrings();
        
        // Inject each custom quest
        for (int i = 0; i < activeQuests.Count && customQuestStartIndex < 10; i++)
        {
            var quest = activeQuests[i];
            int questIndex = customQuestStartIndex + i;
            
            // Calculate text array indices
            int titleIndex = StringArrayStartOffset + questIndex;
            int objectiveIndex = StringArrayStartOffset + TitleSlotsCount + questIndex;
            
            // Store original pointers before overwriting
            if (!OriginalStringPointers.ContainsKey(titleIndex))
            {
                OriginalStringPointers[titleIndex] = (IntPtr)stringArray->StringArray[titleIndex];
            }
            if (!OriginalStringPointers.ContainsKey(objectiveIndex))
            {
                OriginalStringPointers[objectiveIndex] = (IntPtr)stringArray->StringArray[objectiveIndex];
            }
            
            // Allocate and set quest title
            var titlePtr = AllocateString(quest.Title);
            stringArray->StringArray[titleIndex] = (byte*)titlePtr;
            
            // Allocate and set quest objective with progress
            var objectiveText = $"{ObjectivePrefix}{quest.Description}  ({quest.CurrentCount}/{quest.GoalCount})";
            var objectivePtr = AllocateString(objectiveText);
            stringArray->StringArray[objectiveIndex] = (byte*)objectivePtr;
            
            // Set quest metadata in number array
            numberArray->IntArray[QuestTypeIconOffset + questIndex] = GetQuestIcon(quest.Type);
            numberArray->IntArray[ObjectiveCountOffset + questIndex] = 1;
            numberArray->IntArray[ObjectiveProgressOffset + questIndex] = quest.CurrentCount;
            numberArray->IntArray[ButtonCountOffset + questIndex] = 0;
        }
        
        // Update total quest count
        numberArray->IntArray[QuestCountOffset] = totalQuests;
        numberArray->IntArray[QuestListEnabledOffset] = 1;
        
        // Update cache
        LastActiveQuests = new List<QuestData>(activeQuests);
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
    
    private void RestoreOriginalState()
    {
        try
        {
            var instance = RaptureTextModule.Instance();
            if (instance == null) return;
            
            var stringArray = instance->GetStringArrayData(RaptureTextModule.StringArrayType.ToDoList);
            var numberArray = instance->GetNumberArrayData(RaptureTextModule.NumberArrayType.ToDoList);
            
            if (stringArray == null || numberArray == null) return;
            
            // Restore original string pointers
            foreach (var kvp in OriginalStringPointers)
            {
                stringArray->StringArray[kvp.Key] = (byte*)kvp.Value;
            }
            OriginalStringPointers.Clear();
            
            // Restore original quest count
            if (OriginalQuestCount > 0)
            {
                numberArray->IntArray[QuestCountOffset] = OriginalQuestCount;
            }
            
            // Free our allocated strings
            FreeAllocatedStrings();
            
            // Clear cache
            LastActiveQuests = null;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to restore original state: {ex}");
        }
    }
    
    private bool QuestsAreEqual(List<QuestData> list1, List<QuestData> list2)
    {
        if (list1.Count != list2.Count) return false;
        
        for (int i = 0; i < list1.Count; i++)
        {
            var q1 = list1[i];
            var q2 = list2[i];
            
            if (q1.Id != q2.Id ||
                q1.Title != q2.Title ||
                q1.Description != q2.Description ||
                q1.CurrentCount != q2.CurrentCount ||
                q1.GoalCount != q2.GoalCount ||
                q1.Type != q2.Type)
            {
                return false;
            }
        }
        
        return true;
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
        RestoreOriginalState();
    }
}
