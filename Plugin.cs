using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using SocialMorpho.Windows;
using SocialMorpho.Data;

namespace SocialMorpho;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Social Morpho";
    private const string CommandName = "/morpho";
    private const string CommandNameAlt = "/sm";

    private DalamudPluginInterface PluginInterface { get; init; }
    private CommandManager CommandManager { get; init; }
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("SocialMorpho");

    private MainWindow MainWindow { get; init; }
    private QuestManager QuestManager { get; init; }

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] CommandManager commandManager)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        QuestManager = new QuestManager(Configuration);
        MainWindow = new MainWindow(this, QuestManager);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandHandler(OnCommand));
        CommandManager.AddHandler(CommandNameAlt, new CommandHandler(OnCommand));

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

        PluginLog.Information("Social Morpho loaded successfully!");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameAlt);
    }

    private void OnCommand(string command, string args)
    {
        if (args.ToLower() == "config")
        {
            MainWindow.IsOpen = true;
        }
        else
        {
            MainWindow.IsOpen = !MainWindow.IsOpen;
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        MainWindow.IsOpen = true;
    }
}