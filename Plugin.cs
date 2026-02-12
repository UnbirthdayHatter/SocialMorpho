using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SocialMorpho.Data;
using SocialMorpho.Windows;

namespace SocialMorpho;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Social Morpho";
    private const string CommandName = "/morpho";
    private const string CommandNameAlt = "/sm";

    private IDalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("SocialMorpho");

    private MainWindow MainWindow { get; init; }
    private QuestManager QuestManager { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        // Initialize default quests if empty
        if (Configuration.SavedQuests.Count == 0)
        {
            InitializeDefaultQuests();
        }

        QuestManager = new QuestManager(Configuration);
        MainWindow = new MainWindow(this, QuestManager);

        WindowSystem.AddWindow(MainWindow);

        // Register commands
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Social Morpho quest menu"
        });
        CommandManager.AddHandler(CommandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Social Morpho quest menu"
        });

        // Subscribe to draw event
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    private void InitializeDefaultQuests()
    {
        Configuration.SavedQuests.Add(new QuestData
        {
            Id = 1,
            Title = "Get Dotted Three Times",
            Description = "Receive DoT effects from 3 different players",
            Type = QuestType.Social,
            GoalCount = 3,
            CurrentCount = 0,
            Completed = false
        });

        Configuration.SavedQuests.Add(new QuestData
        {
            Id = 2,
            Title = "Hug Four Players",
            Description = "Use the hug emote on 4 different players",
            Type = QuestType.Emote,
            GoalCount = 4,
            CurrentCount = 0,
            Completed = false
        });

        Configuration.SavedQuests.Add(new QuestData
        {
            Id = 3,
            Title = "Social Butterfly",
            Description = "Use 5 social actions with different players",
            Type = QuestType.Social,
            GoalCount = 5,
            CurrentCount = 0,
            Completed = false
        });

        Configuration.Save();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    private void DrawConfigUI()
    {
        MainWindow.IsOpen = true;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameAlt);

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
    }
}