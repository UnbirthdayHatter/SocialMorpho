using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SocialMorpho.Data;
using SocialMorpho.Windows;
using SocialMorpho.Services;

namespace SocialMorpho;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Social Morpho";
    private const string CommandName = "/morpho";
    private const string CommandNameAlt = "/sm";

    public IDalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    private IClientState ClientState { get; init; }
    private IChatGui ChatGui { get; init; }
    public IPluginLog PluginLog { get; init; }
    
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("SocialMorpho");

    private MainWindow MainWindow { get; init; }
    public QuestTrackerWindow QuestTrackerWindow { get; init; }
    private NativeQuestInjector QuestInjector { get; init; }
    public QuestManager QuestManager { get; init; }
    private QuestNotificationService QuestNotificationService { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IChatGui chatGui,
        IPluginLog pluginLog)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        ClientState = clientState;
        ChatGui = chatGui;
        PluginLog = pluginLog;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        // Initialize default quests if empty
        if (Configuration.SavedQuests.Count == 0)
        {
            InitializeDefaultQuests();
        }

        QuestManager = new QuestManager(Configuration);
        
        // Load quests from JSON file
        try
        {
            QuestManager.LoadQuestsFromJson(PluginInterface.ConfigDirectory.FullName);
            PluginLog.Info("Quests loaded from JSON successfully");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error loading quests from JSON: {ex.Message}");
        }

        // Check and reset quests based on schedule
        QuestManager.CheckAndResetQuests();

        QuestNotificationService = new QuestNotificationService(this, ClientState, ChatGui, PluginLog);

        MainWindow = new MainWindow(this, QuestManager);
        QuestTrackerWindow = new QuestTrackerWindow(this, QuestManager);
        QuestInjector = new NativeQuestInjector(this, QuestManager);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(QuestTrackerWindow);
        
        // Show quest tracker if configured
        if (Configuration.ShowQuestTracker)
        {
            QuestTrackerWindow.IsOpen = true;
        }

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

        PluginLog.Info("Social Morpho initialized successfully");
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
        PluginLog.Info("Default quests initialized");
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

        QuestNotificationService?.Dispose();
        QuestInjector?.Dispose();

        PluginLog.Info("Social Morpho disposed");
    }
}