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

        QuestManager = new QuestManager(Configuration);
        MainWindow = new MainWindow(this, QuestManager);

        WindowSystem.AddWindow(MainWindow);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
    }
}