namespace SocialMorpho.Services;

// Intentionally disabled to avoid mutating native ToDoList arrays.
// We render quests via QuestTrackerWindow overlay only.
public sealed class NativeQuestInjector : IDisposable
{
    private readonly Plugin plugin;
    private bool logged;

    public NativeQuestInjector(Plugin plugin, Data.QuestManager questManager)
    {
        this.plugin = plugin;
        if (!this.logged)
        {
            this.plugin.PluginLog.Info("Native quest injection disabled; using overlay tracker only.");
            this.logged = true;
        }
    }

    public void Dispose()
    {
    }
}
