using Dalamud.Plugin.Services;
using System;

namespace SocialMorpho.Services;

public class QuestNotificationService : IDisposable
{
    private Plugin Plugin;
    private IClientState ClientState;
    private IChatGui ChatGui;
    private IPluginLog PluginLog;
    private bool hasShownLoginNotification = false;

    public QuestNotificationService(Plugin plugin, IClientState clientState, IChatGui chatGui, IPluginLog pluginLog)
    {
        Plugin = plugin;
        ClientState = clientState;
        ChatGui = chatGui;
        PluginLog = pluginLog;

        // Subscribe to login event
        ClientState.Login += OnLogin;
    }

    private void OnLogin()
    {
        try
        {
            if (hasShownLoginNotification)
                return;

            hasShownLoginNotification = true;

            var activeQuests = Plugin.QuestManager.GetActiveQuests();

            if (Plugin.Configuration.ShowLoginNotification && activeQuests.Count > 0)
            {
                var message = $"[Social Morpho] You have {activeQuests.Count} active quest{(activeQuests.Count != 1 ? "s" : "")}!";
                ChatGui.Print(message);
                PluginLog.Info(message);
            }

            if (Plugin.Configuration.ShowQuestTrackerOnLogin)
            {
                Plugin.Configuration.ShowQuestTracker = true;
                if (Plugin.QuestTrackerWindow != null)
                {
                    Plugin.QuestTrackerWindow.IsOpen = true;
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error in login notification: {ex.Message}");
        }
    }

    public void Dispose()
    {
        ClientState.Login -= OnLogin;
    }
}
