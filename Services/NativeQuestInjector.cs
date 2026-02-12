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
        var raptureTextModule = RaptureTextModule.Instance();
        if (raptureTextModule == null) return;

        var activeQuests = QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0) return;

        // For now, just log that we would inject quests
        // We need to research the correct way to access ToDoList arrays
        Plugin.PluginLog.Info($"Would inject {activeQuests.Count} custom quests");
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